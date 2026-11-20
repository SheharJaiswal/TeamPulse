using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Identity.UnitTests;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void Validate_AcceptsAmqpAndAmqpsUris()
    {
        new RabbitMqOptions { Uri = "amqp://user:password@localhost:5672/" }.Validate();
        new RabbitMqOptions { Uri = "amqps://user:password@broker.example:5671/" }.Validate();
    }

    [Theory]
    [InlineData("http://localhost:5672/")]
    [InlineData("not-a-uri")]
    [InlineData("amqp:///")]
    public void Validate_RejectsInvalidBrokerUri(string uri)
    {
        var options = new RabbitMqOptions { Uri = uri };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsOversizedRoutingKey()
    {
        var options = new RabbitMqOptions { RoutingKey = new string('a', 256) };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsMissingExchange()
    {
        var options = new RabbitMqOptions { ExchangeName = " " };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}

public sealed class RabbitMqPublisherIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "teampulse.domain.events";
    private const string RoutingKey = "identity.employee.lifecycle";
    private static readonly string[] QueueNames =
    {
        "teampulse.habit.events", "teampulse.challenge.events",
        "teampulse.event-tracking.events", "teampulse.insight.events",
        "teampulse.notification.events"
    };

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
        _connection = await factory.CreateConnectionAsync("TeamPulse.RabbitMqPublisherTests");
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        foreach (var queueName in QueueNames)
        {
            await _channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queueName, ExchangeName, RoutingKey);
        }
    }

    public async Task DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task PublishBatch_ConfirmsAndFansOutTheSameEnvelopeToAllFiveQueues()
    {
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;
        var envelope = JsonSerializer.Serialize(new
        {
            eventId, eventType = "EmployeeDeactivated", schemaVersion = 1,
            occurredAtUtc, correlationId,
            payload = new { employeeId = Guid.NewGuid(), lifecycleVersion = 2 }
        });
        var publisher = CreatePublisher(ExchangeName);
        try
        {
            var result = Assert.Single(await publisher.PublishBatchAsync(new[]
            {
                new OutboxDispatchMessage(eventId, "EmployeeDeactivated", 1, occurredAtUtc,
                    correlationId, envelope, Guid.NewGuid(), 1)
            }, CancellationToken.None));
            Assert.Equal(OutboxPublishOutcome.Accepted, result.Outcome);

            foreach (var queueName in QueueNames)
            {
                var delivery = await _channel!.BasicGetAsync(queueName, autoAck: false);
                Assert.NotNull(delivery);
                Assert.Equal(envelope, Encoding.UTF8.GetString(delivery!.Body.ToArray()));
                Assert.Equal(eventId.ToString("D"), delivery.BasicProperties.MessageId);
                Assert.Equal("EmployeeDeactivated", delivery.BasicProperties.Type);
                Assert.Equal(correlationId.ToString("D"), delivery.BasicProperties.CorrelationId);
                Assert.Equal("application/json", delivery.BasicProperties.ContentType);
                Assert.Equal((byte)2, delivery.BasicProperties.DeliveryMode);
                Assert.Equal(1, Convert.ToInt32(delivery.BasicProperties.Headers!["schemaVersion"]));
                await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
            }
        }
        finally { await publisher.DisposeAsync(); }
    }

    [Fact]
    public async Task PublishBatch_WhenNoQueueIsBound_ReturnsSystemicFailure()
    {
        var publisher = CreatePublisher("teampulse.unrouted.events");
        try
        {
            var message = new OutboxDispatchMessage(Guid.NewGuid(), "EmployeeReactivated", 1,
                DateTime.UtcNow, Guid.NewGuid(), "{\"eventId\":\"unrouted\"}", Guid.NewGuid(), 1);
            var result = Assert.Single(await publisher.PublishBatchAsync(new[] { message }, CancellationToken.None));
            Assert.Equal(OutboxPublishOutcome.SystemicFailure, result.Outcome);
            Assert.Equal("RabbitMqPublishFailed", result.ErrorCode);
        }
        finally { await publisher.DisposeAsync(); }
    }

    private RabbitMqPublisher CreatePublisher(string exchangeName) =>
        new(new RabbitMqOptions
        {
            Uri = _container.GetConnectionString(),
            ExchangeName = exchangeName,
            RoutingKey = RoutingKey
        });
}
