using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace EventTracking.Infrastructure.Persistence;
public sealed class EventTrackingOutboxStore(IDbContextFactory<EventTrackingDbContext> contextFactory, TimeProvider timeProvider) : IEventTrackingOutboxStore
{
    public async Task<IReadOnlyList<EventTrackingOutboxDispatchMessage>> ClaimBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10 || leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = await db.EventTrackingOutboxMessages.FromSqlInterpolated($@"
SELECT * FROM event_tracking_outbox_messages
WHERE PublishedAtUtc IS NULL AND DeadLetteredAtUtc IS NULL
  AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= {now})
  AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= {now})
ORDER BY CreatedAtUtc, EventId LIMIT {batchSize} FOR UPDATE SKIP LOCKED").ToListAsync(cancellationToken);
        var result = new List<EventTrackingOutboxDispatchMessage>(rows.Count);
        foreach (var row in rows)
        {
            var token = Guid.NewGuid();
            if (!row.TryAcquireLease(token, now, now.Add(leaseDuration))) continue;
            result.Add(new EventTrackingOutboxDispatchMessage(row.EventId, row.EventType, row.SchemaVersion,
                row.OccurredAtUtc, row.CorrelationId, row.Payload, token, row.AttemptCount));
        }
        if (result.Count > 0) await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }
    public Task<bool> MarkPublishedAsync(Guid id, Guid token, DateTime atUtc, CancellationToken ct) => UpdateAsync(id, row => row.TryMarkPublished(token, atUtc), ct);
    public Task<bool> ScheduleRetryAsync(Guid id, Guid token, DateTime attemptedAtUtc, DateTime retryAtUtc, string code, CancellationToken ct) => UpdateAsync(id, row => row.TryScheduleRetry(token, attemptedAtUtc, retryAtUtc, code), ct);
    public Task<bool> QuarantineAsync(Guid id, Guid token, DateTime atUtc, string code, CancellationToken ct) => UpdateAsync(id, row => row.TryQuarantine(token, atUtc, code), ct);
    private async Task<bool> UpdateAsync(Guid id, Func<EventTrackingOutboxMessage, bool> transition, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var row = await db.EventTrackingOutboxMessages.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (row is null || !transition(row)) return false;
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }
}
