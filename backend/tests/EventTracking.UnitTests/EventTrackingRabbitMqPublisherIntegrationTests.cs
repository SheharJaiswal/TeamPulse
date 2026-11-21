using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Infrastructure.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class EventTrackingRabbitMqPublisherIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "habit.activity.recorded";
    private const string QueueName = "teampulse.activity.events";
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management")
        .WithUsername("teampulse")
        .WithPassword("local_rabbitmq_password")
        .Build();
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var factory = new ConnectionFactory { Uri = new Uri(_container.GetConnectionString()) };
        _connection = await factory.CreateConnectionAsync("TeamPulse.EventTracking.PublisherTests");
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(QueueName, ExchangeName, RoutingKey);
    }

    public async Task DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task PublishBatch_ConfirmsPersistentVersionedEnvelope()
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        const string envelope = "{\"eventId\":\"event\",\"eventType\":\"HabitActivityRecorded\",\"schemaVersion\":1,\"payload\":{}}";
        await using var publisher = CreatePublisher(RoutingKey);
        var result = Assert.Single(await publisher.PublishBatchAsync(new[]
        {
            new EventTrackingOutboxDispatchMessage(eventId, "HabitActivityRecorded", 1,
                DateTime.UtcNow, correlationId, envelope, Guid.NewGuid(), 1)
        }, CancellationToken.None));
        Assert.Equal(EventTrackingPublishOutcome.Accepted, result.Outcome);

        var delivery = await _channel!.BasicGetAsync(QueueName, autoAck: false);
        Assert.NotNull(delivery);
        Assert.Equal(envelope, Encoding.UTF8.GetString(delivery!.Body.ToArray()));
        Assert.Equal(eventId.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.Equal("HabitActivityRecorded", delivery.BasicProperties.Type);
        Assert.Equal(correlationId.ToString("D"), delivery.BasicProperties.CorrelationId);
        Assert.Equal("application/json", delivery.BasicProperties.ContentType);
        Assert.Equal(DeliveryModes.Persistent, delivery.BasicProperties.DeliveryMode);
        Assert.Equal(1, Convert.ToInt32(delivery.BasicProperties.Headers!["schemaVersion"]));
        await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
    }

    [Fact]
    public async Task PublishBatch_WhenRoutingKeyHasNoBinding_ReturnsSystemicFailure()
    {
        await using var publisher = CreatePublisher("unrouted.activity.event");
        var result = Assert.Single(await publisher.PublishBatchAsync(new[]
        {
            new EventTrackingOutboxDispatchMessage(Guid.NewGuid(), "HabitActivityRecorded", 1,
                DateTime.UtcNow, Guid.NewGuid(), "{\"payload\":{}}", Guid.NewGuid(), 1)
        }, CancellationToken.None));
        Assert.Equal(EventTrackingPublishOutcome.SystemicFailure, result.Outcome);
        Assert.Equal("RabbitMqPublishFailed", result.ErrorCode);
    }


    [Fact]
    public async Task PublishBatch_WhenSameEventIsRetried_PreservesStableMessageId()
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        const string envelope = "{\\"eventId\\":\\"stable\\",\\"eventType\\":\\"HabitActivityRecorded\\",\\"schemaVersion\\":1,\\"payload\\":{}}";
        var message = new EventTrackingOutboxDispatchMessage(eventId, "HabitActivityRecorded", 1,
            DateTime.UtcNow, correlationId, envelope, Guid.NewGuid(), 1);
        await using var publisher = CreatePublisher(RoutingKey);

        var first = Assert.Single(await publisher.PublishBatchAsync(new[] { message }, CancellationToken.None));
        var retry = Assert.Single(await publisher.PublishBatchAsync(new[] { message with { LeaseToken = Guid.NewGuid(), AttemptCount = 2 } }, CancellationToken.None));

        Assert.Equal(EventTrackingPublishOutcome.Accepted, first.Outcome);
        Assert.Equal(EventTrackingPublishOutcome.Accepted, retry.Outcome);
        var firstDelivery = await _channel!.BasicGetAsync(QueueName, autoAck: false);
        var retryDelivery = await _channel.BasicGetAsync(QueueName, autoAck: false);
        Assert.NotNull(firstDelivery);
        Assert.NotNull(retryDelivery);
        Assert.Equal(eventId.ToString("D"), firstDelivery!.BasicProperties.MessageId);
        Assert.Equal(firstDelivery.BasicProperties.MessageId, retryDelivery!.BasicProperties.MessageId);
        Assert.Equal(envelope, Encoding.UTF8.GetString(firstDelivery.Body.ToArray()));
        Assert.Equal(envelope, Encoding.UTF8.GetString(retryDelivery.Body.ToArray()));
        await _channel.BasicAckAsync(firstDelivery.DeliveryTag, multiple: false);
        await _channel.BasicAckAsync(retryDelivery.DeliveryTag, multiple: false);
    }

    private EventTrackingRabbitMqPublisher CreatePublisher(string routingKey) =>
        new(new EventTrackingRabbitMqPublisherOptions
        {
            Uri = _container.GetConnectionString(),
            ExchangeName = ExchangeName,
            RoutingKey = routingKey
        });
}
