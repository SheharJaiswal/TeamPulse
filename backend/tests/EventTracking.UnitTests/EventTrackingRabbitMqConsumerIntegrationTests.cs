using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
using EventTracking.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class EventTrackingRabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "event-tracking.employee.lifecycle.dead";
    private const string QueueName = "teampulse.event-tracking.events";
    private const string DeadLetterQueueName = "teampulse.event-tracking.events.dlq";
    private const string FailureClassificationHeader = "x-teampulse-failure-classification";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management")
        .WithUsername("teampulse")
        .WithPassword("local_rabbitmq_password")
        .Build();

    private readonly List<(EventTrackingRabbitMqConsumer Consumer, ServiceProvider Provider)> _consumers = new();
    private IConnection? _connection;
    private IChannel? _channel;
    private string? _brokerUri;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _brokerUri = _container.GetConnectionString();
        await OpenTestChannelAsync();

        await _channel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        await _channel.QueueDeclareAsync(
            DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" });
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
            QueueName, durable: true, exclusive: false, autoDelete: false, arguments: sourceArguments);
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

        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_AcknowledgesOnlyAfterProjectionProcessorCompletes()
    {
        var processor = new BlockingProcessor();
        await StartConsumerAsync(processor);
        await PublishAsync("{\"eventId\":\"ack-after-commit\"}");

        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(await _channel!.BasicGetAsync(QueueName, autoAck: false));

        processor.Release.TrySetResult(true);
        await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(async () => await GetMessageCountAsync(QueueName) == 0);
        Assert.Equal(1, processor.CallCount);
    }

    [Theory]
    [InlineData(ProjectionProcessingOutcome.Invalid, "InvalidEvent")]
    [InlineData(ProjectionProcessingOutcome.UnsupportedSchemaVersion, "UnsupportedSchemaVersion")]
    public async Task Consumer_ConfirmsDeadLetterBeforeAcknowledgingTerminalOutcome(
        ProjectionProcessingOutcome outcome, string expectedClassification)
    {
        const string body = "{\"eventId\":\"terminal-event\"}";
        await StartConsumerAsync(new FixedOutcomeProcessor(outcome));
        await PublishAsync(body);

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal(expectedClassification, deadLetter.Classification);
        await WaitUntilAsync(async () => await GetMessageCountAsync(QueueName) == 0);
    }

    [Fact]
    public async Task Consumer_RetriesTransientFailureAndAcknowledgesSuccessfulAttempt()
    {
        var processor = new TransientThenSuccessProcessor();
        await StartConsumerAsync(processor, maxAttempts: 3);
        await PublishAsync("{\"eventId\":\"transient-retry\"}");

        await WaitUntilAsync(() => Task.FromResult(Volatile.Read(ref processor.CallCount) == 2));
        await WaitUntilAsync(async () => await GetMessageCountAsync(QueueName) == 0);
        Assert.Equal(2, processor.CallCount);
        Assert.Equal(0u, await GetMessageCountAsync(DeadLetterQueueName));
    }

    [Fact]
    public async Task Consumer_DeadLettersAfterTransientRetryLimitIsExhausted()
    {
        const string body = "{\"eventId\":\"retry-exhausted\"}";
        var processor = new AlwaysTransientProcessor();
        await StartConsumerAsync(processor, maxAttempts: 2);
        await PublishAsync(body);

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal("TransientRetryExhausted", deadLetter.Classification);
        Assert.Equal(2, processor.CallCount);
    }

    [Fact]
    public async Task Consumer_DoesNotLogRawBodyOptionalNoteCredentialsOrBrokerUri()
    {
        const string note = "PRIVATE_OPTIONAL_NOTE_SENTINEL";
        const string body = "{\"eventId\":\"privacy-check\",\"note\":\"PRIVATE_OPTIONAL_NOTE_SENTINEL\",\"payload\":\"PRIVATE_EVENT_BODY_SENTINEL\"}";
        var logger = new CapturingLogger();
        await StartConsumerAsync(new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid), logger: logger);
        await PublishAsync(body);
        await WaitForDeadLetterAsync();

        var renderedLogs = string.Join(Environment.NewLine, logger.Entries);
        Assert.DoesNotContain(body, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(note, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_EVENT_BODY_SENTINEL", renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("local_rabbitmq_password", renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(_brokerUri!, renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consumer_LeavesSourceUnacknowledgedWhenDeadLetterRouteIsUnavailable()
    {
        const string body = "{\"eventId\":\"unroutable-dlq\"}";
        var status = await StartConsumerAsync(
            new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid),
            reconnectDelayMilliseconds: 60000);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
        var completedSessions = status.CompletedSessionCount;

        await _channel!.QueueUnbindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);
        await PublishAsync(body);

        await WaitUntilAsync(() => Task.FromResult(!status.IsConnected));
        await WaitUntilAsync(() => Task.FromResult(status.CompletedSessionCount > completedSessions));
        var redelivered = await _channel.BasicGetAsync(QueueName, autoAck: false);
        Assert.NotNull(redelivered);
        Assert.Equal(body, Encoding.UTF8.GetString(redelivered!.Body.ToArray()));
        await _channel.BasicAckAsync(redelivered.DeliveryTag, multiple: false);
        Assert.Equal(0u, await GetMessageCountAsync(DeadLetterQueueName));
    }

    private async Task<EventTrackingRabbitMqConsumerStatus> StartConsumerAsync(
        IEmployeeStatusProjectionProcessor processor,
        int maxAttempts = 3,
        int reconnectDelayMilliseconds = 5000,
        ILogger<EventTrackingRabbitMqConsumer>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        var provider = services.BuildServiceProvider();

        var options = new EventTrackingRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = _brokerUri!,
            QueueName = QueueName,
            PrefetchCount = 1,
            MaxProcessingAttempts = maxAttempts,
            RetryBaseDelayMilliseconds = 1,
            RetryMaxDelayMilliseconds = 5,
            ReconnectDelayMilliseconds = reconnectDelayMilliseconds,
            ConnectionTimeoutMilliseconds = 1000
        };
        options.Validate();

        var status = new EventTrackingRabbitMqConsumerStatus();
        var consumer = new EventTrackingRabbitMqConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            status,
            logger ?? NullLogger<EventTrackingRabbitMqConsumer>.Instance);
        _consumers.Add((consumer, provider));
        await consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
        return status;
    }

    private async Task OpenTestChannelAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_brokerUri!) };
        _connection = await factory.CreateConnectionAsync("TeamPulse.EventTracking.ConsumerTests");
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
            ExchangeName, RoutingKey, mandatory: true,
            basicProperties: properties, body: Encoding.UTF8.GetBytes(body));
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
                    classification = value switch
                    {
                        string text => text,
                        byte[] bytes => Encoding.UTF8.GetString(bytes),
                        _ => value?.ToString()
                    };
                var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                return (body, classification);
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("EventTracking dead-letter message was not received.");
    }

    private async Task<uint> GetMessageCountAsync(string queueName)
    {
        var declaration = await _channel!.QueueDeclarePassiveAsync(queueName);
        return declaration.MessageCount;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("The expected EventTracking consumer state was not reached.");
    }

    private sealed class FixedOutcomeProcessor(ProjectionProcessingOutcome outcome)
        : IEmployeeStatusProjectionProcessor
    {
        public Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken) =>
            Task.FromResult(outcome);
    }

    private sealed class BlockingProcessor : IEmployeeStatusProjectionProcessor
    {
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            Completed.TrySetResult(true);
            return ProjectionProcessingOutcome.Applied;
        }
    }

    private sealed class TransientThenSuccessProcessor : IEmployeeStatusProjectionProcessor
    {
        public int CallCount;
        public Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref CallCount) == 1)
                throw new InvalidOperationException("Injected transient wrapper.", new TimeoutException("Injected timeout."));
            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }
    }

    private sealed class AlwaysTransientProcessor : IEmployeeStatusProjectionProcessor
    {
        public int CallCount;
        public Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            throw new TimeoutException("Injected transient failure.");
        }
    }

    private sealed class CapturingLogger : ILogger<EventTrackingRabbitMqConsumer>
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(formatter(state, exception));
            if (exception is not null) Entries.Enqueue(exception.ToString());
        }
    }
}
