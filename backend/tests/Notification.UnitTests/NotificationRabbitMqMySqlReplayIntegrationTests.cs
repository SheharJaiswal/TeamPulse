using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Notification.Application;
using Notification.Application.Abstractions;
using Notification.Application.Contracts;
using Notification.Domain.Entities;
using Notification.Infrastructure.Messaging;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Notification.UnitTests;

public sealed class NotificationRabbitMqMySqlReplayIntegrationTests(
    NotificationMySqlFixture databaseFixture) : IClassFixture<NotificationMySqlFixture>, IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "notification.employee.lifecycle.dead";
    private const string QueueName = "teampulse.notification.events";
    private const string DeadLetterQueueName = "teampulse.notification.events.dlq";

    private readonly ReplaySignals _signals = new();
    private RabbitMqContainer? _broker;
    private string _brokerUri = string.Empty;
    private ServiceProvider? _services;
    private NotificationRabbitMqConsumer? _consumer;
    private NotificationRabbitMqConsumerStatus? _consumerStatus;
    private IConnection? _adminConnection;
    private IChannel? _adminChannel;

    public async Task InitializeAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _broker = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-management")
            .WithUsername("teampulse")
            .WithPassword("local_rabbitmq_password")
            .WithPortBinding(hostPort, 5672)
            .Build();
        await _broker.StartAsync();
        _brokerUri = _broker.GetConnectionString();

        await OpenAdminChannelAsync();
        await DeclareTopologyAsync();
    }

    public async Task DisposeAsync()
    {
        if (_consumer is not null)
        {
            await _consumer.StopAsync(CancellationToken.None);
            _consumer.Dispose();
        }
        _services?.Dispose();
        if (_adminChannel is not null) await _adminChannel.DisposeAsync();
        if (_adminConnection is not null) await _adminConnection.DisposeAsync();
        if (_broker is not null) await _broker.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_ReplaysCommittedMessageAfterBrokerInterruption_AndMySqlInboxDeduplicatesIt()
    {
        var employeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await CreateProductionProcessorServicesAsync();
        await StartConsumerAsync();

        var envelope = new EmployeeLifecycleEnvelope
        {
            EventId = eventId,
            EventType = "EmployeeDeactivated",
            SchemaVersion = 1,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Payload = new EmployeeLifecyclePayload
            {
                EmployeeId = employeeId,
                ActorEmployeeId = Guid.NewGuid(),
                Source = "hr-admin-api",
                PreviousStatus = "active",
                NewStatus = "inactive",
                LifecycleVersion = 3,
                ReasonCode = "policy_change",
                Note = "This note must not be persisted."
            }
        };
        var body = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await PublishAsync(body);

        // The production processor has committed the projection and inbox row, but the
        // wrapper deliberately holds the callback before it can return and acknowledge.
        await _signals.FirstCommitCompleted.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await _broker!.StopAsync();
        await _signals.FirstDeliveryCancelled.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await WaitUntilAsync(() => Task.FromResult(!_consumerStatus!.IsConnected));

        // Restart the same broker container: the durable quorum queue and its unacked
        // delivery survive, so the consumer receives the original event again.
        await _broker.StartAsync();
        await WaitUntilAsync(() => Task.FromResult(_consumerStatus!.IsConnected), 30);
        await _signals.DuplicateReplayObserved.Task.WaitAsync(TimeSpan.FromSeconds(20));

        await using var database = await _services!.GetRequiredService<IDbContextFactory<NotificationDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == eventId));
        var inboxEvent = await database.ProcessedDomainEvents.SingleAsync(x => x.EventId == eventId);
        Assert.Equal(employeeId, inboxEvent.EmployeeId);
        Assert.Equal(3, inboxEvent.LifecycleVersion);

        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(3, projection.LifecycleVersion);

        await ReopenAdminChannelAsync();
        Assert.Equal(0u, (await _adminChannel!.QueueDeclarePassiveAsync(QueueName)).MessageCount);
        Assert.Equal(0u, (await _adminChannel.QueueDeclarePassiveAsync(DeadLetterQueueName)).MessageCount);
        Assert.Equal(2, _signals.DeliveryCount);
    }

    private async Task CreateProductionProcessorServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<NotificationDbContext>(options =>
            options.UseMySql(databaseFixture.ConnectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<EmployeeStatusProjectionStore>();
        services.AddScoped<IEmployeeStatusProjectionStore>(sp =>
            sp.GetRequiredService<EmployeeStatusProjectionStore>());
        services.AddScoped<IEmployeeStatusProjectionReader>(sp =>
            sp.GetRequiredService<EmployeeStatusProjectionStore>());
        services.AddScoped<EmployeeStatusProjectionProcessor>();
        services.AddScoped<IEmployeeStatusProjectionProcessor>(sp =>
            new CommitThenBlockProcessor(sp.GetRequiredService<EmployeeStatusProjectionProcessor>(), _signals));

        _services = services.BuildServiceProvider();
        await using var database = await _services.GetRequiredService<IDbContextFactory<NotificationDbContext>>()
            .CreateDbContextAsync();
        await database.Database.MigrateAsync();
    }

    private async Task StartConsumerAsync()
    {
        var options = new NotificationRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = _brokerUri,
            QueueName = QueueName,
            PrefetchCount = 1,
            MaxProcessingAttempts = 3,
            RetryBaseDelayMilliseconds = 1,
            RetryMaxDelayMilliseconds = 5,
            ReconnectDelayMilliseconds = 250,
            ConnectionTimeoutMilliseconds = 1000,
            HeartbeatSeconds = 1
        };
        options.Validate();
        _consumerStatus = new NotificationRabbitMqConsumerStatus();
        _consumer = new NotificationRabbitMqConsumer(
            _services!.GetRequiredService<IServiceScopeFactory>(),
            options,
            _consumerStatus,
            _services.GetRequiredService<ILogger<NotificationRabbitMqConsumer>>());
        await _consumer.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Task.FromResult(_consumerStatus.IsConnected), 20);
    }

    private async Task OpenAdminChannelAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_brokerUri) };
        _adminConnection = await factory.CreateConnectionAsync("TeamPulse.Notification.MySqlReplayTests");
        _adminChannel = await _adminConnection.CreateChannelAsync();
    }

    private async Task ReopenAdminChannelAsync()
    {
        if (_adminChannel is not null) await _adminChannel.DisposeAsync();
        if (_adminConnection is not null) await _adminConnection.DisposeAsync();
        await OpenAdminChannelAsync();
    }

    private async Task DeclareTopologyAsync()
    {
        await _adminChannel!.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _adminChannel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        await _adminChannel.QueueDeclareAsync(
            DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" });
        await _adminChannel.QueueBindAsync(DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

        var arguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = 5,
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey,
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish"
        };
        await _adminChannel.QueueDeclareAsync(
            QueueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
        await _adminChannel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);
    }

    private async Task PublishAsync(string body)
    {
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };
        await _adminChannel!.BasicPublishAsync(
            ExchangeName, RoutingKey, mandatory: true,
            basicProperties: properties, body: Encoding.UTF8.GetBytes(body));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("The expected Notification broker replay state was not reached.");
    }

    private sealed class ReplaySignals
    {
        private int _deliveryCount;
        public int DeliveryCount => Volatile.Read(ref _deliveryCount);
        public TaskCompletionSource<bool> FirstCommitCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> FirstDeliveryCancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> DuplicateReplayObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int NextDelivery() => Interlocked.Increment(ref _deliveryCount);
    }

    private sealed class CommitThenBlockProcessor(
        EmployeeStatusProjectionProcessor productionProcessor,
        ReplaySignals signals) : IEmployeeStatusProjectionProcessor
    {
        public async Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
        {
            var delivery = signals.NextDelivery();
            var outcome = await productionProcessor.ProcessAsync(message, cancellationToken);
            if (delivery == 1)
            {
                Assert.Equal(ProjectionProcessingOutcome.Applied, outcome);
                signals.FirstCommitCompleted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    signals.FirstDeliveryCancelled.TrySetResult(true);
                    throw;
                }
            }

            Assert.Equal(ProjectionProcessingOutcome.Duplicate, outcome);
            signals.DuplicateReplayObserved.TrySetResult(true);
            return outcome;
        }
    }
}
