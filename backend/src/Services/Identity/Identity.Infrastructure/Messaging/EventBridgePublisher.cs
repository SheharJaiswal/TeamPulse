using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Runtime;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Messaging;

public sealed class EventBridgePublisher(IAmazonEventBridge client, string eventBusName) : IEventPublisher
{
    private const string Source = "teampulse.identity";
    private static readonly Meter PublisherMeter = new("TeamPulse.Identity.EventBridgePublisher", "1.0.0");
    private static readonly Histogram<double> PutEventsDuration =
        PublisherMeter.CreateHistogram<double>("teampulse.identity.outbox.put_events.duration", "ms");
    private static readonly Counter<long> FailedEntryCounter =
        PublisherMeter.CreateCounter<long>("teampulse.identity.outbox.put_events.failed_entries");

    public async Task<IReadOnlyList<OutboxPublishResult>> PublishBatchAsync(
        IReadOnlyList<OutboxDispatchMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(messages));

        var request = new PutEventsRequest
        {
            Entries = messages.Select(message => new PutEventsRequestEntry
            {
                EventBusName = eventBusName,
                Source = Source,
                DetailType = message.EventType,
                Time = message.OccurredAtUtc,
                Detail = message.Detail
            }).ToList()
        };

        PutEventsResponse response;
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            response = await client.PutEventsAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AmazonServiceException exception)
        {
            var outcome = ClassifyRequestFailure(exception);
            var code = SafeCode(exception.ErrorCode, "EventBridgeRequestFailed");
            var failed = messages.Select(message =>
                new OutboxPublishResult(message.EventId, outcome, code)).ToArray();
            FailedEntryCounter.Add(failed.Length);
            return failed;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or TimeoutException)
        {
            var failed = messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.RetryableFailure,
                    "TransportUnavailable")).ToArray();
            FailedEntryCounter.Add(failed.Length);
            return failed;
        }
        finally
        {
            PutEventsDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        if (response.Entries is null || response.Entries.Count != messages.Count)
        {
            var failed = messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.RetryableFailure,
                    "InvalidPutEventsResponse")).ToArray();
            FailedEntryCounter.Add(failed.Length);
            return failed;
        }

        var mappedFailureCount = response.Entries.Count(entry =>
            !string.IsNullOrWhiteSpace(entry.ErrorCode) ||
            string.IsNullOrWhiteSpace(entry.EventId));
        if (response.FailedEntryCount != mappedFailureCount)
        {
            var inconsistent = messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.RetryableFailure,
                    "InconsistentPutEventsResponse")).ToArray();
            FailedEntryCounter.Add(inconsistent.Length);
            return inconsistent;
        }

        var results = new List<OutboxPublishResult>(messages.Count);
        for (var index = 0; index < messages.Count; index++)
        {
            var entry = response.Entries[index];
            if (string.IsNullOrWhiteSpace(entry.ErrorCode) &&
                !string.IsNullOrWhiteSpace(entry.EventId))
            {
                results.Add(new OutboxPublishResult(
                    messages[index].EventId, OutboxPublishOutcome.Accepted, string.Empty));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.ErrorCode))
            {
                results.Add(new OutboxPublishResult(
                    messages[index].EventId, OutboxPublishOutcome.RetryableFailure,
                    "InvalidPutEventsEntryResponse"));
                continue;
            }

            var errorCode = SafeCode(entry.ErrorCode, "PutEventsEntryFailed");
            results.Add(new OutboxPublishResult(
                messages[index].EventId, ClassifyEntryFailure(errorCode), errorCode));
        }
        FailedEntryCounter.Add(results.Count(result => result.Outcome != OutboxPublishOutcome.Accepted));
        return results;
    }

    private static OutboxPublishOutcome ClassifyRequestFailure(AmazonServiceException exception)
    {
        if (IsSystemicCode(exception.ErrorCode))
            return OutboxPublishOutcome.SystemicFailure;
        var status = (int)exception.StatusCode;
        if (status == 429 || status >= 500 || IsTransientCode(exception.ErrorCode))
            return OutboxPublishOutcome.RetryableFailure;
        return OutboxPublishOutcome.SystemicFailure;
    }

    private static OutboxPublishOutcome ClassifyEntryFailure(string errorCode)
    {
        if (IsSystemicCode(errorCode))
            return OutboxPublishOutcome.SystemicFailure;
        if (IsTransientCode(errorCode))
            return OutboxPublishOutcome.RetryableFailure;
        return OutboxPublishOutcome.PermanentFailure;
    }

    private static bool IsSystemicCode(string? code) =>
        code is "AccessDeniedException" or "ResourceNotFoundException" or
            "NotAuthorizedForSourceException" or "NotAuthorizedForDetailTypeException" or
            "UnrecognizedClientException" or "InvalidClientTokenId" or "InvalidSignatureException";

    private static bool IsTransientCode(string? code) =>
        code is "InternalFailure" or "ThrottlingException" or "ServiceUnavailableException" or
            "RequestTimeout" or "RequestTimeoutException" or "RequestLimitExceeded";

    private static string SafeCode(string? code, string fallback)
    {
        if (string.IsNullOrWhiteSpace(code))
            return fallback;
        var safe = new string(code.Where(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-').Take(100).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
