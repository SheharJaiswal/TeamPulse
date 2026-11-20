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

public sealed class ChallengeRabbitMqConsumerReconnectTests
{
    [Fact]
    public async Task Consumer_ReconnectsWhenBrokerBecomesAvailableAfterStartup()
    {
        using var portListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        portListener.Start();
        var port = ((System.Net.IPEndPoint)portListener.LocalEndpoint).Port;
        portListener.Stop();

        await using var container = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.1-management")
            .WithUsername("teampulse")
            .WithPassword("local_rabbitmq_password")
            .WithPortBinding(port, 5672)
            .Build();

        var options = ChallengeRabbitMqConsumerOptionsTests.ValidOptions();
        options.Uri = $"amqp://teampulse:local_rabbitmq_password@127.0.0.1:{port}/";
        options.ReconnectDelayMilliseconds = 250;

        var processor = new ReconnectRecordingProcessor();
        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeStatusProjectionProcessor>(processor);
        using var provider = services.BuildServiceProvider();
        var status = new ChallengeRabbitMqConsumerStatus();
        var consumer = new ChallengeRabbitMqConsumer(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            status,
            NullLogger<ChallengeRabbitMqConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            // Initial connection attempts fail. RabbitMQ becoming available must not require
            // restarting the Challenge host.
            await Task.Delay(500);
            await container.StartAsync();

            var factory = new ConnectionFactory { Uri = new Uri(options.Uri) };
            await using var connection = await factory.CreateConnectionAsync("TeamPulse.Challenge.ReconnectTests");
            await using var channel = await connection.CreateChannelAsync();

            const string exchange = "teampulse.domain.events";
            const string routingKey = "identity.employee.lifecycle";
            const string dlx = "teampulse.domain.events.dlx";
            const string deadRoutingKey = "challenge.employee.lifecycle.dead";
            const string queue = "teampulse.challenge.events";
            const string deadQueue = "teampulse.challenge.events.dlq";

            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            await channel.ExchangeDeclareAsync(dlx, ExchangeType.Direct, durable: true, autoDelete: false);
            await channel.QueueDeclareAsync(deadQueue, true, false, false,
                new Dictionary<string, object?> { ["x-queue-type"] = "quorum" });
            await channel.QueueBindAsync(deadQueue, dlx, deadRoutingKey);

            var arguments = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = 5,
                ["x-dead-letter-exchange"] = dlx,
                ["x-dead-letter-routing-key"] = deadRoutingKey,
                ["x-dead-letter-strategy"] = "at-least-once",
                ["x-overflow"] = "reject-publish"
            };
            await channel.QueueDeclareAsync(queue, true, false, false, arguments);
            await channel.QueueBindAsync(queue, exchange, routingKey);

            await WaitUntilAsync(() => Task.FromResult(status.IsConnected));
            var properties = new BasicProperties
            {
                MessageId = Guid.NewGuid().ToString("D"),
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };
            await channel.BasicPublishAsync(
                exchange, routingKey, mandatory: true,
                basicProperties: properties, body: Encoding.UTF8.GetBytes("{\"reconnect\":true}"));

            await processor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(status.IsConnected);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            consumer.Dispose();
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("Challenge consumer did not reconnect to RabbitMQ.");
    }

    private sealed class ReconnectRecordingProcessor : IEmployeeStatusProjectionProcessor
    {
        public TaskCompletionSource<bool> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProjectionProcessingOutcome> ProcessAsync(
            string message, CancellationToken cancellationToken)
        {
            Completed.TrySetResult(true);
            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }
    }
}
