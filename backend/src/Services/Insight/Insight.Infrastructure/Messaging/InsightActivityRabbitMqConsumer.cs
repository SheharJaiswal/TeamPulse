using System.Text;
using Insight.Application;
using Insight.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Insight.Infrastructure.Messaging;

public sealed class InsightActivityRabbitMqConsumer(
    IServiceScopeFactory scopeFactory,
    InsightActivityConsumerOptions options,
    InsightActivityConsumerStatus status,
    ILogger<InsightActivityRabbitMqConsumer> logger) : BackgroundService
{
    private const string DeadLetterExchange = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "insight.activity.recorded.dead";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Insight activity consumer is disabled.");
            return;
        }

        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                status.MarkDisconnected();
                logger.LogWarning("Insight activity consumer session ended ({ErrorType}); reconnecting.",
                    exception.GetType().Name);
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunSessionAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.Uri, UriKind.Absolute),
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            RequestedHeartbeat = TimeSpan.FromSeconds(5),
            ClientProvidedName = "TeamPulse.Insight.ActivityConsumer"
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await using var deadLetterChannel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true), stoppingToken);
        status.MarkConnected();
        await channel.BasicQosAsync(0, options.PrefetchCount, false, stoppingToken);

        var sessionFailed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                string message;
                try
                {
                    message = new UTF8Encoding(false, true).GetString(delivery.Body.Span);
                }
                catch (DecoderFallbackException)
                {
                    await DeadLetterAsync(deadLetterChannel, channel, delivery, "InvalidEvent", stoppingToken);
                    return;
                }

                ProjectionProcessingOutcome outcome = ProjectionProcessingOutcome.Invalid;
                Exception? lastException = null;
                for (var attempt = 1; attempt <= options.MaxProcessingAttempts; attempt++)
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var processor = scope.ServiceProvider.GetRequiredService<ActivityRecordedEventProcessor>();
                        outcome = await processor.ProcessAsync(message, stoppingToken);
                        lastException = null;
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        lastException = exception;
                        if (attempt < options.MaxProcessingAttempts)
                            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), stoppingToken);
                    }
                }

                if (lastException is not null)
                {
                    logger.LogWarning("Insight activity processing failed after bounded retries ({ErrorType}); message will be redelivered.",
                        lastException.GetType().Name);
                    sessionFailed.TrySetResult(true);
                    return;
                }

                if (outcome is ProjectionProcessingOutcome.Applied or ProjectionProcessingOutcome.Duplicate)
                {
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                    return;
                }

                var classification = outcome == ProjectionProcessingOutcome.UnsupportedSchemaVersion
                    ? "UnsupportedSchemaVersion"
                    : "InvalidEvent";
                await DeadLetterAsync(deadLetterChannel, channel, delivery, classification, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning("Insight activity delivery could not be completed ({ErrorType}); session will recover the message.",
                    exception.GetType().Name);
                sessionFailed.TrySetResult(true);
            }
        };

        await channel.BasicConsumeAsync(options.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested && connection.IsOpen && channel.IsOpen &&
                   deadLetterChannel.IsOpen && !sessionFailed.Task.IsCompleted)
            {
                await Task.Delay(250, stoppingToken);
            }
        }
        finally
        {
            status.MarkDisconnected();
        }
    }

    private static async Task DeadLetterAsync(
        IChannel deadLetterChannel,
        IChannel sourceChannel,
        BasicDeliverEventArgs delivery,
        string classification,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            MessageId = delivery.BasicProperties.MessageId,
            CorrelationId = delivery.BasicProperties.CorrelationId,
            Type = delivery.BasicProperties.Type,
            ContentType = delivery.BasicProperties.ContentType ?? "application/octet-stream",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                ["x-teampulse-failure-classification"] = classification
            }
        };

        await deadLetterChannel.BasicPublishAsync(
            DeadLetterExchange, DeadLetterRoutingKey, mandatory: true,
            basicProperties: properties, body: delivery.Body, cancellationToken: cancellationToken);
        await sourceChannel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
    }
}
