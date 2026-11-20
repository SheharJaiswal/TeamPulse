using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class OutboxMessageStore(
    IDbContextFactory<IdentityDbContext> contextFactory,
    TimeProvider timeProvider) : IOutboxMessageStore
{
    public async Task<IReadOnlyList<OutboxDispatchMessage>> ClaimBatchAsync(
        int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var rows = await database.OutboxMessages
            .FromSqlInterpolated($@"
SELECT * FROM outbox_messages
WHERE PublishedAtUtc IS NULL
  AND DeadLetteredAtUtc IS NULL
  AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {nowUtc})
  AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= {nowUtc})
ORDER BY CreatedAtUtc, EventId
LIMIT {batchSize}
FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var claimed = new List<OutboxDispatchMessage>(rows.Count);
        foreach (var row in rows)
        {
            var token = Guid.NewGuid();
            if (!row.TryAcquireLease(token, nowUtc, nowUtc.Add(leaseDuration)))
                continue;
            claimed.Add(new OutboxDispatchMessage(
                row.EventId, row.EventType, row.SchemaVersion, row.OccurredAtUtc,
                row.CorrelationId, row.Payload, token, row.AttemptCount));
        }

        if (claimed.Count > 0)
            await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public Task<bool> MarkPublishedAsync(Guid eventId, Guid leaseToken, DateTime publishedAtUtc,
        CancellationToken cancellationToken) =>
        UpdateOwnedAsync(eventId, message => message.TryMarkPublished(leaseToken, publishedAtUtc), cancellationToken);

    public Task<bool> ScheduleRetryAsync(Guid eventId, Guid leaseToken, DateTime nextAttemptAtUtc,
        string errorCode, CancellationToken cancellationToken) =>
        UpdateOwnedAsync(eventId, message => message.TryScheduleRetry(leaseToken, nextAttemptAtUtc, errorCode), cancellationToken);

    public Task<bool> QuarantineAsync(Guid eventId, Guid leaseToken, DateTime deadLetteredAtUtc,
        string errorCode, CancellationToken cancellationToken) =>
        UpdateOwnedAsync(eventId, message => message.TryQuarantine(leaseToken, deadLetteredAtUtc, errorCode), cancellationToken);

    public Task<bool> ReleaseForSystemicFailureAsync(Guid eventId, Guid leaseToken, DateTime retryAtUtc,
        string errorCode, CancellationToken cancellationToken) =>
        UpdateOwnedAsync(eventId, message => message.TryScheduleRetry(leaseToken, retryAtUtc, errorCode), cancellationToken);

    public async Task<OutboxQueueSnapshot> GetQueueSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var pending = database.OutboxMessages.Where(message =>
            message.PublishedAtUtc == null && message.DeadLetteredAtUtc == null);

        var pendingCount = await pending.LongCountAsync(cancellationToken);
        var oldestPending = await pending
            .Select(message => (DateTime?)message.CreatedAtUtc)
            .MinAsync(cancellationToken);
        var activeLeases = await pending.LongCountAsync(
            message => message.LeaseExpiresAtUtc.HasValue && message.LeaseExpiresAtUtc > nowUtc,
            cancellationToken);
        var expiredLeases = await pending.LongCountAsync(
            message => message.LeaseExpiresAtUtc.HasValue && message.LeaseExpiresAtUtc <= nowUtc,
            cancellationToken);
        var quarantinedCount = await database.OutboxMessages.LongCountAsync(
            message => message.DeadLetteredAtUtc != null, cancellationToken);

        return new OutboxQueueSnapshot(
            pendingCount, oldestPending, activeLeases, expiredLeases, quarantinedCount);
    }

    private async Task<bool> UpdateOwnedAsync(Guid eventId, Func<OutboxMessage, bool> transition,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var message = await database.OutboxMessages.SingleOrDefaultAsync(
            item => item.EventId == eventId, cancellationToken);
        if (message is null || !transition(message))
            return false;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
