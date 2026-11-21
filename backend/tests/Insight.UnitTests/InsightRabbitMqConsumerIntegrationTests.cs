using System.Collections.Concurrent;
using System.Text;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;
using Insight.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Insight.UnitTests;

public sealed class InsightRabbitMqConsumerIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "insight.employee.lifecycle.dead";
    private const string QueueName = "teampulse.insight.events";
    private const string DeadLetterQueueName = "teampulse.insight.events.dlq";
    private const string FailureClassificationHeader = "x-teampulse-failure-classification";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management")
        .WithUsername("teampulse")
        .WithPassword("local_rabbitmq_password")
        .Build();
    private readonly List<(InsightRabbitMqConsumer Consumer, ServiceProvider Provider)> _consumers = new();
    private string _brokerUri = string.Empty;
    private IConnection? _connection;
    private IChannel? _channel;

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
    public async Task Consumer_AcknowledgesOnlyAfterProcessorCompletes()
    {
        var processor = new BlockingProcessor();
        await StartConsumerAsync(processor);
        await PublishAsync("{\"eventId\":\"ack-after-commit\"}");

        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(await _channel!.BasicGetAsync(QueueName, autoAck: false));

        processor.Release.TrySetResult(true);
        await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // With prefetch=1, this second delivery cannot be processed until the first
        // delivery's acknowledgement has completed on the consumer channel.
        await PublishAsync("{\"eventId\":\"after-ack-sentinel\"}");
        await processor.SentinelProcessed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(await _channel!.BasicGetAsync(QueueName, autoAck: false));
        Assert.Null(await _channel.BasicGetAsync(DeadLetterQueueName, autoAck: false));
    }

    [Fact]
    public async Task Consumer_DoesNotLogRawPayloadNotesCredentialsOrConnectionStrings()
    {
        const string note = "privacy-sentinel-note";
        const string credential = "privacy-sentinel-password";
        const string body = "{\\"eventId\\":\\"privacy-sentinel-event\\",\\"note\\":\\"privacy-sentinel-note\\",\\"credential\\":\\"privacy-sentinel-password\\",\\"connectionString\\":\\"amqp://user:privacy-sentinel-password@private-host:5672/private\\"}";
        var processor = new BlockingProcessor();
        var logger = new CapturingLogger<InsightRabbitMqConsumer>();
        await StartConsumerAsync(processor, logger: logger);
        await PublishAsync(body);

        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        processor.Release.TrySetResult(true);
        await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // The sentinel delivery proves the sensitive message was acknowledged before
        // the consumer moved on; the logger snapshot is taken after both callbacks.
        await PublishAsync("{\\"eventId\\":\\"after-privacy-sentinel\\"}");
        await processor.SentinelProcessed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var logs = string.Join(Environment.NewLine, logger.Entries);
        Assert.DoesNotContain(body, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("privacy-sentinel-event", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(note, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(_brokerUri, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Consumer_PublishesTerminalOutcomeToDlqWithClassification()
    {
        const string body = "{\"eventId\":\"invalid-insight-event\"}";
        await StartConsumerAsync(new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid));
        await PublishAsync(body);

        var deadLetter = await WaitForDeadLetterAsync();
        Assert.Equal(body, deadLetter.Body);
        Assert.Equal("InvalidEvent", deadLetter.Classification);
        Assert.Null(await _channel!.BasicGetAsync(QueueName, autoAck: false));
    }

    [Fact]
    public async Task Consumer_LeavesSourceUnacknowledgedWhenDlqRouteIsUnavailable()
    {
        const string body = "{\"eventId\":\"unroutable-insight-dlq\"}";
        var status = await StartConsumerAsync(
            new FixedOutcomeProcessor(ProjectionProcessingOutcome.Invalid),
            reconnectDelayMilliseconds: 60000);
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
        await _channel.BasicAckAsync(redelivered.DeliveryTag, multiple: false);
        Assert.Null(await _channel.BasicGetAsync(DeadLetterQueueName, autoAck: false));
    }

    private async Task<InsightRabbitMqConsumerStatus> StartConsumerAsync(
        IEmployeeStatusProjectionProcessor processor,
        int reconnectDelayMilliseconds = 250,
        ILogger<InsightRabbitMqConsumer>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        var provider = services.BuildServiceProvider();
        var options = new InsightRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = _brokerUri,
            QueueName = QueueName,
            PrefetchCount = 1,
            MaxProcessingAttempts = 2,
            RetryBaseDelayMilliseconds = 1,
            RetryMaxDelayMilliseconds = 5,
            ReconnectDelayMilliseconds = reconnectDelayMilliseconds,
            ConnectionTimeoutMilliseconds = 1000
        };
        var status = new InsightRabbitMqConsumerStatus();
        var consumer = new InsightRabbitMqConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            status,
            logger ?? LoggerFactory.Create(_ => { }).CreateLogger<InsightRabbitMqConsumer>());
        _consumers.Add((consumer, provider));
        await consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
        return status;
    }

    private async Task OpenTestChannelAsync()
    {
        _connection = await new ConnectionFactory { Uri = new Uri(_brokerUri) }
            .CreateConnectionAsync("TeamPulse.Insight.ConsumerTests");
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
                var headers = delivery.BasicProperties.Headers;
                string? classification = null;
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
        throw new TimeoutException("Insight dead-letter message was not received.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(25);
        }
        throw new TimeoutException("The expected Insight consumer state was not reached.");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(formatter(state, exception));
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
        public TaskCompletionSource<bool> SentinelProcessed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
        {
            if (message.Contains("after-ack-sentinel", StringComparison.Ordinal))
            {
                SentinelProcessed.TrySetResult(true);
                return ProjectionProcessingOutcome.Applied;
            }

            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            Completed.TrySetResult(true);
            return ProjectionProcessingOutcome.Applied;
        }
    }
}
