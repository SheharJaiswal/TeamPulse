using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace EventTracking.Application.Abstractions;
public sealed record EventTrackingOutboxDispatchMessage(
    Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
    Guid CorrelationId, string Payload, Guid LeaseToken, int AttemptCount);
public interface IEventTrackingOutboxStore
{
    Task<IReadOnlyList<EventTrackingOutboxDispatchMessage>> ClaimBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<bool> MarkPublishedAsync(Guid eventId, Guid leaseToken, DateTime publishedAtUtc, CancellationToken cancellationToken);
    Task<bool> ScheduleRetryAsync(Guid eventId, Guid leaseToken, DateTime attemptedAtUtc, DateTime retryAtUtc, string errorCode, CancellationToken cancellationToken);
    Task<bool> QuarantineAsync(Guid eventId, Guid leaseToken, DateTime deadLetteredAtUtc, string errorCode, CancellationToken cancellationToken);
}
public enum EventTrackingPublishOutcome { Accepted = 0, RetryableFailure = 1, SystemicFailure = 2 }
public sealed record EventTrackingPublishResult(Guid EventId, EventTrackingPublishOutcome Outcome, string ErrorCode);
public interface IEventTrackingPublisher
{
    Task<IReadOnlyList<EventTrackingPublishResult>> PublishBatchAsync(IReadOnlyList<EventTrackingOutboxDispatchMessage> messages, CancellationToken cancellationToken);
}
