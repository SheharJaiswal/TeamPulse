using System.Data;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;

namespace Insight.Infrastructure.Persistence;

public sealed class MySqlEmployeeStatusProjectionStore(IDbContextFactory<InsightActivityDbContext> contextFactory) : IEmployeeStatusProjectionStore
{
    private const int MaxAttempts = 25;

    public async Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        if (processedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Processed timestamp must be UTC.", nameof(processedAtUtc));

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                if (await db.EmployeeStatusProcessedEvents.AnyAsync(x => x.EventId == lifecycleEvent.EventId, cancellationToken))
                    return ProjectionProcessingOutcome.Duplicate;

                var projection = await db.EmployeeStatusProjections
                    .SingleOrDefaultAsync(x => x.EmployeeId == lifecycleEvent.EmployeeId, cancellationToken);
                var outcome = ProjectionProcessingOutcome.Stale;
                if (projection is null)
                {
                    db.EmployeeStatusProjections.Add(new InsightEmployeeStatusProjection
                    {
                        EmployeeId = lifecycleEvent.EmployeeId, IsActive = lifecycleEvent.IsActive,
                        LifecycleVersion = lifecycleEvent.LifecycleVersion, UpdatedAtUtc = processedAtUtc
                    });
                    outcome = ProjectionProcessingOutcome.Applied;
                }
                else if (lifecycleEvent.LifecycleVersion > projection.LifecycleVersion)
                {
                    projection.IsActive = lifecycleEvent.IsActive;
                    projection.LifecycleVersion = lifecycleEvent.LifecycleVersion;
                    projection.UpdatedAtUtc = processedAtUtc;
                    outcome = ProjectionProcessingOutcome.Applied;
                }

                db.EmployeeStatusProcessedEvents.Add(new InsightEmployeeStatusProcessedEvent
                {
                    EventId = lifecycleEvent.EventId, EmployeeId = lifecycleEvent.EmployeeId,
                    EventType = lifecycleEvent.EventType, SchemaVersion = lifecycleEvent.SchemaVersion,
                    LifecycleVersion = lifecycleEvent.LifecycleVersion, ProcessedAtUtc = processedAtUtc
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return outcome;
            }
            catch (Exception exception) when (FindMySqlErrorNumber(exception) is 1062 or 1205 or 1213)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errorNumber = FindMySqlErrorNumber(exception);
                if (errorNumber == 1062)
                {
                    await using var verify = await contextFactory.CreateDbContextAsync(cancellationToken);
                    if (await verify.EmployeeStatusProcessedEvents.AnyAsync(x => x.EventId == lifecycleEvent.EventId, cancellationToken))
                        return ProjectionProcessingOutcome.Duplicate;
                }
                if (attempt == MaxAttempts - 1) throw;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(20 * Math.Min(attempt + 1, 20)), cancellationToken);
        }
        throw new InvalidOperationException("Insight lifecycle projection concurrency retry budget was exhausted.");
    }

    private static int? FindMySqlErrorNumber(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is MySqlException mysqlException)
                return mysqlException.Number;
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions)
                    pending.Push(inner);
            if (current.InnerException is not null)
                pending.Push(current.InnerException);
        }
        return null;
    }
}