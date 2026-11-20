using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Challenge.Infrastructure.Messaging;

public sealed class ChallengeRabbitMqConsumer(
    IServiceScopeFactory scopeFactory,
    ChallengeRabbitMqConsumerOptions options,
    ChallengeRabbitMqConsumerStatus status,
    ILogger<ChallengeRabbitMqConsumer> logger) : BackgroundService
{
    private const string ConnectionName = "TeamPulse.Challenge.EmployeeLifecycleConsumer";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Challenge employee lifecycle consumer is disabled.");
            return;
        }

        options.Validate();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Do not log exception messages: broker exceptions can contain connection details.
                logger.LogWarning(
                    "Challenge RabbitMQ consumer session ended ({ErrorType}); reconnecting.",
                    exception.GetType().Name);
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromMilliseconds(options.ReconnectDelayMilliseconds), stoppingToken);
        }
    }

    private async Task RunConsumerSessionAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.Uri, UriKind.Absolute),
            ClientProvidedName = ConnectionName,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false
        };

        await using var connection = await factory.CreateConnectionAsync(ConnectionName, stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, options.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
            await HandleDeliveryAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        status.MarkConnected();
        logger.LogInformation("Challenge employee lifecycle consumer connected to queue {QueueName}.", options.QueueName);

        try
        {
            // Automatic recovery is deliberately disabled. A closed connection/channel exits this
            // session and the hosted service reconnects with bounded delay.
            while (!stoppingToken.IsCancellationRequested && connection.IsOpen && channel.IsOpen)
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
        finally
        {
            status.MarkDisconnected();
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        try
        {
            string message;
            try
            {
                message = StrictUtf8.GetString(delivery.Body.Span);
            }
            catch (DecoderFallbackException)
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                logger.LogWarning(
                    "Challenge rejected a non-UTF8 lifecycle message {MessageId}.",
                    delivery.BasicProperties.MessageId);
                return;
            }

            var outcome = await ProcessWithRetriesAsync(message, delivery.BasicProperties.MessageId, stoppingToken);
            if (!outcome.HasValue || outcome == ProjectionProcessingOutcome.Invalid)
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, stoppingToken);
                logger.LogWarning(
                    "Challenge routed lifecycle message {MessageId} to its DLQ after validation or processing failure.",
                    delivery.BasicProperties.MessageId);
                return;
            }

            // Applied, Stale and Duplicate are all committed processing outcomes.
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Leave the delivery unacknowledged. Closing the channel returns it to the broker.
        }
        catch (Exception exception)
        {
            // If ack/nack fails, close the channel so RabbitMQ redelivers rather than losing
            // a delivery whose database commit outcome may already be durable.
            logger.LogWarning(
                "Challenge could not complete acknowledgement for message {MessageId} ({ErrorType}); closing the channel for redelivery.",
                delivery.BasicProperties.MessageId,
                exception.GetType().Name);
            try
            {
                await channel.CloseAsync(cancellationToken: CancellationToken.None);
            }
            catch
            {
                // The connection may already be closed; broker-side unacked delivery recovery applies.
            }
        }
    }

    private async Task<ProjectionProcessingOutcome?> ProcessWithRetriesAsync(
        string message,
        string? messageId,
        CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= options.MaxProcessingAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionProcessor>();
                return await processor.ProcessAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var transient = IsTransient(exception);
                logger.LogWarning(
                    "Challenge lifecycle processing failed for message {MessageId} on attempt {Attempt} ({ErrorType}, transient={Transient}).",
                    messageId,
                    attempt,
                    exception.GetType().Name,
                    transient);

                if (!transient || attempt == options.MaxProcessingAttempts)
                    return null;

                await Task.Delay(GetRetryDelay(attempt), stoppingToken);
            }
        }

        return null;
    }

    private TimeSpan GetRetryDelay(int attempt)
    {
        var exponentialCeiling = options.RetryBaseDelayMilliseconds * Math.Pow(2, attempt - 1);
        var boundedCeiling = Math.Min(exponentialCeiling, options.RetryMaxDelayMilliseconds);
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * boundedCeiling);
    }

    private static bool IsTransient(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var innerException in aggregate.InnerExceptions)
            {
                if (IsTransient(innerException))
                    return true;
            }
        }

        // EF Core commonly wraps provider failures in DbUpdateException. Inspect the complete
        // inner-exception chain so transient MySQL/network faults are retried rather than DLQ'd.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException or IOException or SocketException)
                return true;

            if (current is MySqlException mysqlException &&
                mysqlException.Number is 1040 or 1205 or 1213 or 2002 or 2003 or 2006 or 2013 or 2055)
                return true;
        }

        return false;
    }
}
