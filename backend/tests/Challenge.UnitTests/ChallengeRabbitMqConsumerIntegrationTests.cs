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

    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management")
        .WithUsername("teampulse")
        .WithPassword("local_rabbitmq_password")
        .Build();

    private readonly List<(ChallengeRabbitMqConsumer Consumer, ServiceProvider Provider)> _consumers = new();
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
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
        var processor = new AlwaysTransientProcessor();
        await StartConsumerAsync(processor, maxAttempts: 2);
        await PublishAsync("{\"eventId\":\"exhausted\"}");

        await WaitForDeadLetterAsync();
        Assert.Equal(2, processor.CallCount);
    }

    private async Task<ChallengeRabbitMqConsumerStatus> StartConsumerAsync(
        IEmployeeStatusProjectionProcessor processor,
        int maxAttempts = 3,
        int reconnectDelayMilliseconds = 5000)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        var provider = services.BuildServiceProvider();

        var options = ChallengeRabbitMqConsumerOptionsTests.ValidOptions();
        options.Uri = _container.GetConnectionString();
        options.MaxProcessingAttempts = maxAttempts;
        options.RetryBaseDelayMilliseconds = 1;
        options.RetryMaxDelayMilliseconds = 5;
        options.ReconnectDelayMilliseconds = reconnectDelayMilliseconds;
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

        var factory = new ConnectionFactory { Uri = new Uri(_container.GetConnectionString()) };
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

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException("The expected consumer state was not reached.");
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
}
