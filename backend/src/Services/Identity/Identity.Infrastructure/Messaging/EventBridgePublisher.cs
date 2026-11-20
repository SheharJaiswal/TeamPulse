using System;
using System.Collections.Generic;
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
            return messages.Select(message =>
                new OutboxPublishResult(message.EventId, outcome, code)).ToArray();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or TimeoutException)
        {
            return messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.RetryableFailure,
                    "TransportUnavailable")).ToArray();
        }

        if (response.Entries is null || response.Entries.Count != messages.Count)
            return messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.RetryableFailure,
                    "InvalidPutEventsResponse")).ToArray();

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

            var errorCode = SafeCode(entry.ErrorCode, "PutEventsEntryFailed");
            results.Add(new OutboxPublishResult(
                messages[index].EventId, ClassifyEntryFailure(errorCode), errorCode));
        }
        return results;
    }

    private static OutboxPublishOutcome ClassifyRequestFailure(AmazonServiceException exception)
    {
        if (IsSystemicCode(exception.ErrorCode))
            return OutboxPublishOutcome.SystemicFailure;
        var status = (int)exception.StatusCode;
        if (status == 0 || status == 429 || status >= 500 || IsTransientCode(exception.ErrorCode))
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
