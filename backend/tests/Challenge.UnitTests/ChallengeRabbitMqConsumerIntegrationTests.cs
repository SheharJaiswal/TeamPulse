using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Challenge.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeRabbitMqConsumerOptionsTests
{
    [Fact]
    public void Validate_AllowsDisabledConsumerWithoutBrokerSettings() =>
        new ChallengeRabbitMqConsumerOptions().Validate();

    [Theory]
    [InlineData("http://localhost:5672/")]
    [InlineData("not-a-uri")]
    [InlineData("amqp:///")]
    public void Validate_RejectsInvalidBrokerUri(string uri)
    {
        var options = ValidOptions();
        options.Uri = uri;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_RequiresSingleInFlightDelivery()
    {
        var options = ValidOptions();
        options.PrefetchCount = 2;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsInvalidRetryBounds()
    {
        var options = ValidOptions();
        options.RetryMaxDelayMilliseconds = options.RetryBaseDelayMilliseconds - 1;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(30001)]
    public void Validate_RejectsInvalidConnectionTimeout(int timeoutMilliseconds)
    {
        var options = ValidOptions();
        options.ConnectionTimeoutMilliseconds = timeoutMilliseconds;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Status_MarkSessionEndedIncrementsCompletedSessionCount()
    {
        var status = new ChallengeRabbitMqConsumerStatus();

        Assert.Equal(0, status.CompletedSessionCount);
        status.MarkSessionEnded();
        status.MarkSessionEnded();

        Assert.Equal(2, status.CompletedSessionCount);
    }

    [Fact]
    public void Status_TracksConnectionAttemptsAndEstablishedSessions()
    {
        var status = new ChallengeRabbitMqConsumerStatus();

        status.MarkConnectionAttempt();
        status.MarkConnectionAttempt();
        status.MarkConnected();
        status.MarkConnected();

        Assert.Equal(2, status.ConnectionAttemptCount);
        Assert.Equal(2, status.EstablishedSessionCount);
        Assert.True(status.IsConnected);
    }

    [Fact]
    public void Status_RecordsFailureStageAndExceptionTypesWithoutExceptionMessages()
    {
        var status = new ChallengeRabbitMqConsumerStatus();
        const string sensitiveMessage = "amqp://teampulse:secret-password@broker:5672/";

        status.MarkConnectionAttempt();
        status.MarkSessionStage("consumer-registration");
        status.RecordSessionFailure(new InvalidOperationException(
            sensitiveMessage,
            new System.Net.Sockets.SocketException()));

        Assert.Equal("consumer-registration", status.LastSessionFailureStage);
        Assert.Equal("InvalidOperationException -> SocketException", status.LastSessionFailureTypes);
        Assert.DoesNotContain(sensitiveMessage, status.LastSessionFailureTypes, StringComparison.Ordinal);
    }

    internal static ChallengeRabbitMqConsumerOptions ValidOptions() => new()
    {
        Enabled = true,
        Uri = "amqp://teampulse:local_rabbitmq_password@localhost:5672/",
        QueueName = "teampulse.challenge.events",
        PrefetchCount = 1,
        MaxProcessingAttempts = 3,
        RetryBaseDelayMilliseconds = 1,
        RetryMaxDelayMilliseconds = 5,
        ReconnectDelayMilliseconds = 250
    };
}

public sealed class ChallengeRabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "challenge.employee.lifecycle.dead";
    private const string QueueName = "teampulse.challenge.events";
    private const string DeadLetterQueueName = "teampulse.challenge.events.dlq";
    private const string FailureClassificationHeader = "x-teampulse-failure-classification";

    private readonly RabbitMqContainer _container;
    private readonly string _brokerUri;
    private readonly List<(ChallengeRabbitMqConsumer Consumer, ServiceProvider Provider)> _consumers = new();
    private IConnection? _connection;
    private IChannel? _channel;

    public ChallengeRabbitMqConsumerIntegrationTests()
    {
        // Keep the host endpoint stable across Testcontainers StopAsync/StartAsync.
        // A dynamically allocated port can change after restart while the consumer keeps
        // reconnecting to the original URI, producing endless connection-refused failures.
        using var portListener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        portListener.Start();
        var brokerPort = ((System.Net.IPEndPoint)portListener.LocalEndpoint).Port;
        portListener.Stop();

        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-management")
            .WithUsername("teampulse")
            .WithPassword("local_rabbitmq_password")
            .WithPortBinding(brokerPort, 5672)
            .Build();
        _brokerUri = $"amqp://teampulse:local_rabbitmq_password@127.0.0.1:{brokerPort}/";
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Assert.Equal(_brokerUri, _container.GetConnectionString());
        await OpenTestChannelAsync();

        await _channel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);

        var deadLetterArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum"
        };
        await _channel.QueueDeclareAsync(
            DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: deadLetterArguments);
        await _channel.QueueBindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

        var sourceArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = 5,
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey,
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish"
        };
        await _channel.QueueDeclareAsync(
            QueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: sourceArguments);
        await _channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);
    }

    public async Task DisposeAsync()
    {
        foreach (var (consumer, provider) in _consumers)
        {
            await consumer.StopAsync(CancellationToken.None);
            consumer.Dispose();
            provider.Dispose();
        }

        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_DoesNotAcknowledgeUntilProcessorCompletes()
    {
        var processor = new BlockingProcessor();
        await StartConsumerAsync(processor);
        await PublishAsync("{\"eventId\":\"test\"}");

        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var unacknowledged = await _channel!.BasicGetAsync(QueueName, autoAck: false);
        Assert.Null(unacknowledged);

        processor.Release.TrySetResult(true);
        await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(150);

        var redelivered = await _channel.BasicGetAsync(QueueName, autoAck: false);
        Assert.Null(redelivered);
    }

    [Fact]
    public async Task Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck()
    {
        const string committedBody = "{\"eventId\":\"commit-before-ack\"}";
        const string sentinelBody = "{\"eventId\":\"after-replay\"}";
        var processor = new CommitBeforeAckReplayProcessor(committedBody, sentinelBody);
        var status = await StartConsumerAsync(
            processor,
            reconnectDelayMilliseconds: 250,
            connectionTimeoutMilliseconds: 1000);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
        var completedSessionsBeforeBrokerStop = status.CompletedSessionCount;
        var connectionAttemptsBeforeBrokerStop = status.ConnectionAttemptCount;
        var establishedSessionsBeforeBrokerStop = status.EstablishedSessionCount;

        await PublishAsync(committedBody);
        await processor.FirstCommitCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The processor has recorded its inbox/projection commit but deliberately has not
        // returned, so the consumer cannot yet acknowledge the source delivery. Broker-session
        // cancellation must release the in-flight callback without acknowledging that commit.
        await _container.StopAsync();
        await WaitUntilAsync(() => Task.FromResult(!status.IsConnected));
        await processor.SessionCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(() => Task.FromResult(
            status.CompletedSessionCount > completedSessionsBeforeBrokerStop));

        // The completed-session signal is raised only after the old channels and connection
        // have been disposed. Restarting now cannot race the previous session's teardown.
        await _container.StartAsync();
        await OpenTestChannelAsync();
        // The duplicate outcome proves a new broker session has re-established a usable consumer
        // channel. Avoid polling IsConnected here: it is a point-in-time health signal, while replay
        // and the following sentinel prove the behavior required by this acceptance test.
        var replayDeadline = DateTime.UtcNow.AddSeconds(30);
        while (!processor.DuplicateReplayObserved.Task.IsCompleted && DateTime.UtcNow < replayDeadline)
            await Task.Delay(25);

        if (!processor.DuplicateReplayObserved.Task.IsCompleted)
        {
            var queueState = await _channel!.QueueDeclarePassiveAsync(QueueName);
            throw new TimeoutException(
                "Challenge replay did not occur. Connection attempts before/after: " +
                connectionAttemptsBeforeBrokerStop + "/" + status.ConnectionAttemptCount +
                "; established sessions before/after: " +
                establishedSessionsBeforeBrokerStop + "/" + status.EstablishedSessionCount +
                "; completed sessions: " + status.CompletedSessionCount +
                "; source queue messages: " + queueState.MessageCount +
                "; consumers: " + queueState.ConsumerCount +
                "; last session failure stage/types: " + status.LastSessionFailureStage +
                "/" + status.LastSessionFailureTypes +
                "; current session stage: " + status.CurrentSessionStage + ".");
        }

        await processor.DuplicateReplayObserved.Task;

        // With prefetch=1, this sentinel can be processed only after the duplicate replay
        // completed and its source delivery was acknowledged.
        await PublishAsync(sentinelBody);
        await processor.SentinelProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(2, processor.CommittedMessageCalls);
        Assert.Null(await _channel!.BasicGetAsync(DeadLetterQueueName, autoAck: false));
    }

    [Theory]
    [InlineData(ProjectionProcessingOutcome.Invalid, "InvalidEvent")]
    [InlineData(ProjectionProcessingOutcome.UnsupportedSchemaVersion, "UnsupportedSchemaVersion")]
    [InlineData(ProjectionProcessingOutcome.InvalidProducer, "InvalidProducer")]
    [InlineData(ProjectionProcessingOutcome.DuplicateEventIntegrityViolation, "DuplicateEventIntegrityViolation")]
    public async Task Consumer_PublishesTerminalValidationOutcomesToClassifiedDeadLetterQueue(
        ProjectionProcessingOutcome outcome,
        string expectedClassification)
    {
        await StartConsumerAsync(new FixedOutcomeProcessor(outcome));
        await PublishAsync("{\"eventId\":\"invalid\"}");

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal("{\"eventId\":\"invalid\"}", deadLetter.Body);
        Assert.Equal(expectedClassification, deadLetter.Classification);
    }

    [Fact]
    public async Task Consumer_LeavesOriginalUnacknowledgedWhenDeadLetterPublishCannotBeConfirmed()
    {
        const string body = "{\"eventId\":\"unroutable-dlq\"}";
        var status = await StartConsumerAsync(
            new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid),
            reconnectDelayMilliseconds: 60000);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));

        await _channel!.QueueUnbindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);
        await PublishAsync(body);

        // The mandatory publish is returned because no DLQ binding exists. The consumer must
        // close its source channel without acknowledging, allowing RabbitMQ to requeue the body.
        await WaitUntilAsync(() => Task.FromResult(!status.IsConnected));
        var redelivered = await _channel.BasicGetAsync(QueueName, autoAck: false);
        Assert.NotNull(redelivered);
        Assert.Equal(body, Encoding.UTF8.GetString(redelivered!.Body.ToArray()));
        await _channel.BasicAckAsync(redelivered.DeliveryTag, multiple: false);
        Assert.Null(await _channel.BasicGetAsync(DeadLetterQueueName, autoAck: false));
    }

    [Fact]
    public async Task Consumer_ReconnectsAndDeadLettersRedeliveryAfterDeadLetterRouteIsRestored()
    {
        const string body = "{\"eventId\":\"dlq-route-restored\"}";
        var status = await StartConsumerAsync(
            new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid),
            reconnectDelayMilliseconds: 1000);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));

        await _channel!.QueueUnbindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);
        await PublishAsync(body);

        // A mandatory unroutable DLQ publish must end the session without acknowledging
        // the original. Restore the route before the bounded reconnect attempt.
        await WaitUntilAsync(() => Task.FromResult(!status.IsConnected));
        await _channel.QueueBindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal("InvalidEvent", deadLetter.Classification);
    }

    [Fact]
    public async Task Consumer_RetriesTransientFailureThenAcknowledges()
    {
        var processor = new TransientThenSuccessProcessor();
        await StartConsumerAsync(processor, maxAttempts: 3);
        await PublishAsync("{\"eventId\":\"retry\"}");

        await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref processor.CallCount) == 2));
        await Task.Delay(150);

        Assert.Null(await _channel!.BasicGetAsync(QueueName, autoAck: false));
        Assert.Null(await _channel.BasicGetAsync(DeadLetterQueueName, autoAck: false));
        Assert.Equal(2, processor.CallCount);
    }

    [Fact]
    public async Task Consumer_DeadLettersAfterConfiguredTransientRetryLimit()
    {
        const string body = "{\"eventId\":\"exhausted\"}";
        var processor = new AlwaysTransientProcessor();
        await StartConsumerAsync(processor, maxAttempts: 2);
        await PublishAsync(body);

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal("TransientRetryExhausted", deadLetter.Classification);
        Assert.Equal(2, processor.CallCount);
    }

    [Fact]
    public async Task Consumer_DeadLettersNonTransientProcessingFailureWithoutRetry()
    {
        const string body = "{\"eventId\":\"terminal\"}";
        var processor = new AlwaysNonTransientProcessor();
        await StartConsumerAsync(processor, maxAttempts: 3);
        await PublishAsync(body);

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal("NonTransientProcessingFailure", deadLetter.Classification);
        Assert.Equal(1, processor.CallCount);
    }

    [Fact]
    public async Task Consumer_LeavesOriginalUnacknowledgedWhenRetryExhaustionDeadLetterCannotBeConfirmed()
    {
        const string body = "{\"eventId\":\"exhausted-unroutable\"}";
        var processor = new AlwaysTransientProcessor();
        var status = await StartConsumerAsync(
            processor, maxAttempts: 1, reconnectDelayMilliseconds: 60000);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
        var completedSessionsBeforeFailure = status.CompletedSessionCount;

        await _channel!.QueueUnbindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);
        await PublishAsync(body);

        await WaitUntilAsync(() => Task.FromResult(!status.IsConnected));
        await WaitUntilAsync(() => Task.FromResult(
            status.CompletedSessionCount > completedSessionsBeforeFailure));
        var redelivered = await _channel.BasicGetAsync(QueueName, autoAck: false);
        Assert.NotNull(redelivered);
        Assert.Equal(body, Encoding.UTF8.GetString(redelivered!.Body.ToArray()));
        Assert.Equal(1, processor.CallCount);
        await _channel.BasicAckAsync(redelivered.DeliveryTag, multiple: false);
        Assert.Null(await _channel.BasicGetAsync(DeadLetterQueueName, autoAck: false));
    }

    private async Task<ChallengeRabbitMqConsumerStatus> StartConsumerAsync(
        IEmployeeStatusProjectionProcessor processor,
        int maxAttempts = 3,
        int reconnectDelayMilliseconds = 5000,
        int connectionTimeoutMilliseconds = 5000)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        var provider = services.BuildServiceProvider();

        var options = ChallengeRabbitMqConsumerOptionsTests.ValidOptions();
        options.Uri = _brokerUri;
        options.MaxProcessingAttempts = maxAttempts;
        options.RetryBaseDelayMilliseconds = 1;
        options.RetryMaxDelayMilliseconds = 5;
        options.ReconnectDelayMilliseconds = reconnectDelayMilliseconds;
        options.ConnectionTimeoutMilliseconds = connectionTimeoutMilliseconds;
        options.Validate();

        var status = new ChallengeRabbitMqConsumerStatus();
        var consumer = new ChallengeRabbitMqConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            status,
            NullLogger<ChallengeRabbitMqConsumer>.Instance);

        _consumers.Add((consumer, provider));
        await consumer.StartAsync(CancellationToken.None);
        return status;
    }

    private async Task OpenTestChannelAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();

        var factory = new ConnectionFactory { Uri = new Uri(_brokerUri) };
        _connection = await factory.CreateConnectionAsync("TeamPulse.Challenge.ConsumerTests");
        _channel = await _connection.CreateChannelAsync();
    }

    private async Task PublishAsync(string body)
    {
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _channel!.BasicPublishAsync(
            ExchangeName,
            RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(body));
    }

    private async Task<(string Body, string? Classification)> WaitForDeadLetterAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var delivery = await _channel!.BasicGetAsync(DeadLetterQueueName, autoAck: false);
            if (delivery is not null)
            {
                string? classification = null;
                var headers = delivery.BasicProperties.Headers;
                if (headers is not null && headers.TryGetValue(FailureClassificationHeader, out var value))
                {
                    classification = value switch
                    {
                        string text => text,
                        byte[] bytes => Encoding.UTF8.GetString(bytes),
                        _ => value?.ToString()
                    };
                }

                var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                return (body, classification);
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Challenge dead-letter message was not received.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException("The expected consumer state was not reached.");
    }

    private sealed class CommitBeforeAckReplayProcessor(string committedBody, string sentinelBody)
        : IEmployeeStatusProjectionProcessor
    {
        private int _committedMessageCalls;

        public TaskCompletionSource<bool> FirstCommitCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SessionCancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> DuplicateReplayObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SentinelProcessed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CommittedMessageCalls => Volatile.Read(ref _committedMessageCalls);

        public async Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            if (message == committedBody)
            {
                var attempt = Interlocked.Increment(ref _committedMessageCalls);
                if (attempt == 1)
                {
                    // Model the durable inbox/projection transaction committing before the
                    // consumer callback is allowed to return to its BasicAck call.
                    FirstCommitCompleted.TrySetResult(true);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        SessionCancellationObserved.TrySetResult(true);
                        throw;
                    }

                    return ProjectionProcessingOutcome.Applied;
                }

                DuplicateReplayObserved.TrySetResult(true);
                return ProjectionProcessingOutcome.Duplicate;
            }

            if (message == sentinelBody)
            {
                SentinelProcessed.TrySetResult(true);
                return ProjectionProcessingOutcome.Applied;
            }

            throw new InvalidOperationException("Unexpected message in commit-before-ack replay test.");
        }
    }

    private sealed class FixedOutcomeProcessor(ProjectionProcessingOutcome outcome)
        : IEmployeeStatusProjectionProcessor
    {
        public Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken) =>
            Task.FromResult(outcome);
    }

    private sealed class BlockingProcessor : IEmployeeStatusProjectionProcessor
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            Completed.TrySetResult(true);
            return ProjectionProcessingOutcome.Applied;
        }
    }

    private sealed class TransientThenSuccessProcessor : IEmployeeStatusProjectionProcessor
    {
        public int CallCount;

        public Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref CallCount);
            if (call == 1)
                throw new InvalidOperationException(
                    "Injected EF-style wrapper.",
                    new TimeoutException("Injected transient failure."));

            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }
    }

    private sealed class AlwaysTransientProcessor : IEmployeeStatusProjectionProcessor
    {
        public int CallCount;

        public Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            throw new TimeoutException("Injected transient failure.");
        }
    }

    private sealed class AlwaysNonTransientProcessor : IEmployeeStatusProjectionProcessor
    {
        public int CallCount;

        public Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            throw new InvalidOperationException("Injected terminal processing failure.");
        }
    }
}
