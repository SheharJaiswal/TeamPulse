using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Abstractions;

public sealed record OutboxDispatchMessage(
    Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
    Guid CorrelationId, string Detail, Guid LeaseToken, int AttemptCount);

public sealed record OutboxQueueSnapshot(
    long PendingCount,
    DateTime? OldestPendingCreatedAtUtc,
    long ActiveLeaseCount,
    long ExpiredLeaseCount,
    long QuarantinedCount);

public enum OutboxPublishOutcome
{
    Accepted = 0,
    RetryableFailure = 1,
    PermanentFailure = 2,
    SystemicFailure = 3
}

public sealed record OutboxPublishResult(
    Guid EventId, OutboxPublishOutcome Outcome, string ErrorCode);

public interface IEventPublisher
{
    Task<IReadOnlyList<OutboxPublishResult>> PublishBatchAsync(
        IReadOnlyList<OutboxDispatchMessage> messages, CancellationToken cancellationToken);
}

public interface IOutboxMessageStore
{
    Task<IReadOnlyList<OutboxDispatchMessage>> ClaimBatchAsync(
        int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task<bool> MarkPublishedAsync(
        Guid eventId, Guid leaseToken, DateTime publishedAtUtc, CancellationToken cancellationToken);

    Task<bool> ScheduleRetryAsync(
        Guid eventId, Guid leaseToken, DateTime nextAttemptAtUtc, string errorCode,
        CancellationToken cancellationToken);

    Task<bool> QuarantineAsync(
        Guid eventId, Guid leaseToken, DateTime deadLetteredAtUtc, string errorCode,
        CancellationToken cancellationToken);

    Task<bool> ReleaseForSystemicFailureAsync(
        Guid eventId, Guid leaseToken, DateTime retryAtUtc, string errorCode,
        CancellationToken cancellationToken);

    Task<OutboxQueueSnapshot> GetQueueSnapshotAsync(CancellationToken cancellationToken);
}
