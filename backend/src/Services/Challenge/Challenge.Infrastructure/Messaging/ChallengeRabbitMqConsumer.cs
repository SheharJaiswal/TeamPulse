using System;
using System.Collections.Generic;
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
    private const string DeadLetterExchangeName = "teampulse.domain.events.dlx";
    private const string DeadLetterRoutingKey = "challenge.employee.lifecycle.dead";
    private const string FailureClassificationHeader = "x-teampulse-failure-classification";
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

        // This dedicated channel confirms every DLQ publish. Mandatory routing plus tracked
        // publisher confirms makes BasicPublishAsync fail if the DLQ binding is absent or the
        // broker does not accept the dead-letter message.
        await using var deadLetterChannel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            stoppingToken);

        // A broker-session loss must cancel in-flight callbacks as well as end the
        // connection loop. This releases the old session so the hosted service can reconnect.
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var sessionToken = sessionCancellation.Token;

        await channel.BasicQosAsync(0, options.PrefetchCount, false, stoppingToken);

        var callbackSync = new object();
        TaskCompletionSource<bool>? inFlightCallback = null;
        var sessionEnding = false;

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            TaskCompletionSource<bool> callbackCompletion;
            lock (callbackSync)
            {
                // Do not begin new work once this broker session is ending. The delivery
                // remains unacknowledged and is recovered when the channel closes.
                if (sessionEnding)
                    return;

                callbackCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                inFlightCallback = callbackCompletion;
            }

            try
            {
                await HandleDeliveryAsync(channel, deadLetterChannel, delivery, sessionToken);
            }
            finally
            {
                lock (callbackSync)
                {
                    if (ReferenceEquals(inFlightCallback, callbackCompletion))
                        inFlightCallback = null;
                }

                callbackCompletion.TrySetResult(true);
            }
        };

        await channel.BasicConsumeAsync(
            options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        status.MarkConnected();
        logger.LogInformation("Challenge RabbitMQ consumer connected to queue {QueueName}.", options.QueueName);

        try
        {
            // Automatic recovery is deliberately disabled. A closed connection/channel exits this
            // session and the hosted service reconnects with bounded delay.
            while (!stoppingToken.IsCancellationRequested &&
                   connection.IsOpen &&
                   channel.IsOpen &&
                   deadLetterChannel.IsOpen)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
        }
        finally
        {
            Task? callbackToDrain;
            lock (callbackSync)
            {
                sessionEnding = true;
                callbackToDrain = inFlightCallback?.Task;
            }

            // Cancel the processor first, then wait for the active ReceivedAsync handler to
            // unwind before this session disposes its channels/connection or reconnects.
            sessionCancellation.Cancel();
            try
            {
                if (callbackToDrain is not null)
                    await callbackToDrain.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                // Keep reconnect bounded even if a processor ignores cancellation. The old
                // channel is already unavailable; its delivery remains eligible for replay.
                logger.LogWarning(
                    "Challenge RabbitMQ delivery callback did not stop within the session drain timeout ({TimeoutSeconds}s).",
                    10);
            }
            finally
            {
                status.MarkDisconnected();
            }
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        IChannel deadLetterChannel,
        BasicDeliverEventArgs delivery,
        CancellationToken sessionToken)
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
                await PublishDeadLetterAndAcknowledgeAsync(
                    deadLetterChannel, channel, delivery, "InvalidEvent", sessionToken);
                logger.LogWarning(
                    "Challenge dead-lettered lifecycle message {MessageId} with classification {FailureClassification}.",
                    delivery.BasicProperties.MessageId,
                    "InvalidEvent");
                return;
            }

            var processingResult = await ProcessWithRetriesAsync(
                message, delivery.BasicProperties.MessageId, sessionToken);

            var failureClassification = processingResult.FailureClassification;
            if (failureClassification is not null)
            {
                await PublishDeadLetterAndAcknowledgeAsync(
                    deadLetterChannel, channel, delivery, failureClassification, sessionToken);
                logger.LogWarning(
                    "Challenge dead-lettered lifecycle message {MessageId} with classification {FailureClassification}.",
                    delivery.BasicProperties.MessageId,
                    failureClassification);
                return;
            }

            var outcome = processingResult.Outcome
                ?? throw new InvalidOperationException("Challenge processing returned neither an outcome nor a failure classification.");

            failureClassification = outcome switch
            {
                ProjectionProcessingOutcome.Invalid => "InvalidEvent",
                ProjectionProcessingOutcome.UnsupportedSchemaVersion => "UnsupportedSchemaVersion",
                ProjectionProcessingOutcome.InvalidProducer => "InvalidProducer",
                ProjectionProcessingOutcome.DuplicateEventIntegrityViolation => "DuplicateEventIntegrityViolation",
                ProjectionProcessingOutcome.Applied or ProjectionProcessingOutcome.Stale or ProjectionProcessingOutcome.Duplicate => null,
                _ => "NonTransientProcessingFailure"
            };

            if (failureClassification is not null)
            {
                await PublishDeadLetterAndAcknowledgeAsync(
                    deadLetterChannel, channel, delivery, failureClassification, sessionToken);
                logger.LogWarning(
                    "Challenge dead-lettered lifecycle message {MessageId} with classification {FailureClassification}.",
                    delivery.BasicProperties.MessageId,
                    failureClassification);
                return;
            }

            // Applied, Stale and Duplicate are all committed processing outcomes.
            await channel.BasicAckAsync(delivery.DeliveryTag, false, sessionToken);
        }
        catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
        {
            // Leave the delivery unacknowledged. Closing the channel returns it to the broker.
        }
        catch (Exception exception)
        {
            // If DLQ publication/confirmation or the source acknowledgement fails, close the
            // consumer channel so the original delivery remains eligible for redelivery.
            logger.LogWarning(
                "Challenge could not complete delivery handling for message {MessageId} ({ErrorType}); closing the channel for redelivery.",
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

    private static async Task PublishDeadLetterAndAcknowledgeAsync(
        IChannel deadLetterChannel,
        IChannel consumerChannel,
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
                [FailureClassificationHeader] = classification
            }
        };

        await deadLetterChannel.BasicPublishAsync(
            DeadLetterExchangeName,
            DeadLetterRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: delivery.Body,
            cancellationToken: cancellationToken);

        // BasicPublishAsync completes only after a positive broker confirmation because the
        // dedicated channel enables confirmation tracking. Ack the source only after that point.
        await consumerChannel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
    }

    private async Task<ProcessingResult> ProcessWithRetriesAsync(
        string message,
        string? messageId,
        CancellationToken sessionToken)
    {
        for (var attempt = 1; attempt <= options.MaxProcessingAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionProcessor>();
                return new ProcessingResult(
                    await processor.ProcessAsync(message, sessionToken),
                    null);
            }
            catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
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

                if (!transient)
                    return new ProcessingResult(null, "NonTransientProcessingFailure");

                if (attempt == options.MaxProcessingAttempts)
                    return new ProcessingResult(null, "TransientRetryExhausted");

                await Task.Delay(GetRetryDelay(attempt), sessionToken);
            }
        }

        return new ProcessingResult(null, "TransientRetryExhausted");
    }

    private TimeSpan GetRetryDelay(int attempt) =>
        ChallengeRetryDelayPolicy.GetDelay(
            attempt,
            options.RetryBaseDelayMilliseconds,
            options.RetryMaxDelayMilliseconds,
            Random.Shared.NextDouble());

    private sealed record ProcessingResult(
        ProjectionProcessingOutcome? Outcome,
        string? FailureClassification);

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
