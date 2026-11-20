using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Application.Contracts;
using Habit.Infrastructure.Messaging;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Habit.UnitTests;

public sealed class HabitRabbitMqBrokerLossIntegrationTests
    : IClassFixture<HabitMySqlFixture>, IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "habit.employee.lifecycle.dead";
    private const string QueueName = "teampulse.habit.events";
    private const string DeadLetterQueueName = "teampulse.habit.events.dlq";

    private readonly HabitMySqlFixture _mysqlFixture;
    private readonly RabbitMqContainer _rabbitMq;
    private string? _brokerUri;

    public HabitRabbitMqBrokerLossIntegrationTests(HabitMySqlFixture mysqlFixture)
    {
        _mysqlFixture = mysqlFixture;

        using var portListener = new TcpListener(IPAddress.Loopback, 0);
        portListener.Start();
        var brokerPort = ((IPEndPoint)portListener.LocalEndpoint).Port;
        portListener.Stop();

        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-management")
            .WithUsername("teampulse")
            .WithPassword("local_rabbitmq_password")
            .WithPortBinding(brokerPort, 5672)
            .Build();
    }

    private HabitApiFactory? _apiFactory;
    private HttpClient? _apiClient;
    private ServiceProvider? _consumerProvider;
    private HabitRabbitMqConsumer? _consumer;
    private HabitRabbitMqConsumerStatus? _consumerStatus;
    private IConnection? _publisherConnection;
    private IChannel? _publisherChannel;

    public async Task InitializeAsync()
    {
        await _rabbitMq.StartAsync();
        _brokerUri = _rabbitMq.GetConnectionString();
        await OpenPublisherChannelAsync();
        await DeclareTopologyAsync();

        _apiFactory = _mysqlFixture.CreateFactory();
        _apiClient = _apiFactory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_consumer is not null)
        {
            try
            {
                await _consumer.StopAsync(CancellationToken.None);
            }
            finally
            {
                _consumer.Dispose();
            }
        }

        _consumerProvider?.Dispose();
        _apiClient?.Dispose();
        _apiFactory?.Dispose();

        await DisposePublisherChannelAsync();
        await _rabbitMq.DisposeAsync();
    }

    [Fact]
    public async Task Consumer_WhenBrokerConnectionIsLostAfterProjectionCommit_RedeliversAndDeduplicates()
    {
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var message = CreateDeactivationMessage(eventId, employeeId);
        var processor = await StartConsumerAsync();

        try
        {
            await WaitUntilAsync(
                () => _consumerStatus!.IsConnected,
                "Habit consumer did not establish its initial RabbitMQ session.");

            await PublishAsync(message);
            var firstOutcome = await processor.FirstCommitted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(ProjectionProcessingOutcome.Applied, firstOutcome);

            // The application transaction has committed, but the consumer has not returned
            // from the processor and therefore has not acknowledged the delivery.
            await _rabbitMq.StopAsync();
            await WaitUntilAsync(
                () => !_consumerStatus!.IsConnected,
                "Habit consumer did not observe the established-session broker loss.");

            // Let the first delivery return only after RabbitMQ has closed the channel.
            // Its acknowledgement must fail; the durable quorum queue must redeliver it.
            processor.ReleaseFirstDelivery.TrySetResult(true);
            await processor.FirstReturned.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(150);

            await _rabbitMq.StartAsync();
            Assert.Equal(_brokerUri, _rabbitMq.GetConnectionString());
            await OpenPublisherChannelAsync();
            await DeclareTopologyAsync();

            await WaitUntilAsync(
                () => _consumerStatus!.IsConnected,
                "Habit consumer did not reconnect after RabbitMQ became available.");

            var replayOutcome = await processor.SecondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(250);

            Assert.Equal(ProjectionProcessingOutcome.Duplicate, replayOutcome);
            Assert.Equal(2, processor.CallCount);
            Assert.Equal(message, processor.FirstMessage);
            Assert.Equal(message, processor.SecondMessage);
            Assert.True(_consumerStatus!.IsConnected);

            var contextFactory = _apiFactory!.Services.GetRequiredService<IDbContextFactory<HabitDbContext>>();
            await using var database = await contextFactory.CreateDbContextAsync();
            var projection = await database.EmployeeStatusProjections
                .SingleAsync(x => x.EmployeeId == employeeId);
            Assert.False(projection.IsActive);
            Assert.Equal(1, projection.LifecycleVersion);
            Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == eventId));
        }
        finally
        {
            processor.ReleaseFirstDelivery.TrySetResult(true);
        }
    }

    private async Task<CommitThenBlockProcessor> StartConsumerAsync()
    {
        var applicationScopeFactory = _apiFactory!.Services.GetRequiredService<IServiceScopeFactory>();
        var processor = new CommitThenBlockProcessor(applicationScopeFactory);

        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        _consumerProvider = services.BuildServiceProvider();

        var options = HabitRabbitMqConsumerOptionsTests.ValidOptions();
        options.Uri = _brokerUri!;
        options.ReconnectDelayMilliseconds = 250;
        options.RetryBaseDelayMilliseconds = 1;
        options.RetryMaxDelayMilliseconds = 5;
        options.Validate();

        _consumerStatus = new HabitRabbitMqConsumerStatus();
        _consumer = new HabitRabbitMqConsumer(
            _consumerProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            _consumerStatus,
            NullLogger<HabitRabbitMqConsumer>.Instance);

        await _consumer.StartAsync(CancellationToken.None);
        return processor;
    }

    private async Task OpenPublisherChannelAsync()
    {
        await DisposePublisherChannelAsync();

        var factory = new ConnectionFactory { Uri = new Uri(_brokerUri ?? _rabbitMq.GetConnectionString()) };
        _publisherConnection = await factory.CreateConnectionAsync("TeamPulse.Habit.BrokerLossTests");
        _publisherChannel = await _publisherConnection.CreateChannelAsync();
    }

    private async Task DisposePublisherChannelAsync()
    {
        if (_publisherChannel is not null)
        {
            try
            {
                await _publisherChannel.DisposeAsync();
            }
            catch
            {
                // The broker may already have stopped as part of this test.
            }

            _publisherChannel = null;
        }

        if (_publisherConnection is not null)
        {
            try
            {
                await _publisherConnection.DisposeAsync();
            }
            catch
            {
                // The broker may already have stopped as part of this test.
            }

            _publisherConnection = null;
        }
    }

    private async Task DeclareTopologyAsync()
    {
        await _publisherChannel!.ExchangeDeclareAsync(
            ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _publisherChannel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);

        await _publisherChannel.QueueDeclareAsync(
            DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" });
        await _publisherChannel.QueueBindAsync(
            DeadLetterQueueName, DeadLetterExchange, DeadLetterRoutingKey);

        var sourceArguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = 5,
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey,
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish"
        };
        await _publisherChannel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: sourceArguments);
        await _publisherChannel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);
    }

    private async Task PublishAsync(string message)
    {
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString("D"),
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _publisherChannel!.BasicPublishAsync(
            ExchangeName,
            RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message));
    }

    private static string CreateDeactivationMessage(Guid eventId, Guid employeeId) =>
        JsonSerializer.Serialize(new
        {
            eventId,
            eventType = "EmployeeDeactivated",
            schemaVersion = 1,
            occurredAtUtc = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid(),
            payload = new
            {
                employeeId,
                actorEmployeeId = Guid.NewGuid(),
                source = "hr-admin-api",
                previousStatus = "active",
                newStatus = "inactive",
                lifecycleVersion = 1,
                reasonCode = "employment-change"
            }
        });

    private static async Task WaitUntilAsync(Func<bool> condition, string timeoutMessage)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException(timeoutMessage);
    }

    private sealed class CommitThenBlockProcessor(IServiceScopeFactory applicationScopeFactory)
        : IEmployeeStatusProjectionProcessor
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);
        public string? FirstMessage { get; private set; }
        public string? SecondMessage { get; private set; }

        public TaskCompletionSource<ProjectionProcessingOutcome> FirstCommitted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ProjectionProcessingOutcome> FirstReturned { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ProjectionProcessingOutcome> SecondProcessed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseFirstDelivery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProjectionProcessingOutcome> ProcessAsync(
            string message,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);

            await using var scope = applicationScopeFactory.CreateAsyncScope();
            var applicationProcessor = scope.ServiceProvider
                .GetRequiredService<IEmployeeStatusProjectionProcessor>();
            var outcome = await applicationProcessor.ProcessAsync(message, cancellationToken);

            if (call == 1)
            {
                FirstMessage = message;
                FirstCommitted.TrySetResult(outcome);
                await ReleaseFirstDelivery.Task.WaitAsync(cancellationToken);
                FirstReturned.TrySetResult(outcome);
            }
            else if (call == 2)
            {
                SecondMessage = message;
                SecondProcessed.TrySetResult(outcome);
            }

            return outcome;
        }
    }
}
