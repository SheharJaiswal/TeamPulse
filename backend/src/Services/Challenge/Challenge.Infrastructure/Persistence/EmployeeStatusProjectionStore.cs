using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Challenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Challenge.Infrastructure.Persistence;

public sealed class EmployeeStatusProjectionStore(IDbContextFactory<ChallengeDbContext> contextFactory)
    : IEmployeeStatusProjectionStore
{
    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        if (processedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Processing timestamp must be UTC.", nameof(processedAtUtc));

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        database.ProcessedDomainEvents.Add(new ProcessedDomainEvent(
            lifecycleEvent.EventId, lifecycleEvent.EventType, lifecycleEvent.SchemaVersion,
            lifecycleEvent.EmployeeId, lifecycleEvent.LifecycleVersion, processedAtUtc,
            lifecycleEvent.FingerprintAlgorithm, lifecycleEvent.ContentFingerprint));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is MySqlException mysqlException && mysqlException.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            await database.DisposeAsync();

            await using var verification = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await verification.ProcessedDomainEvents.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == lifecycleEvent.EventId, cancellationToken);
            if (existing is null) throw;

            return existing.FingerprintAlgorithm == lifecycleEvent.FingerprintAlgorithm &&
                   string.Equals(existing.ContentFingerprint, lifecycleEvent.ContentFingerprint, StringComparison.Ordinal)
                ? ProjectionProcessingOutcome.Duplicate
                : ProjectionProcessingOutcome.DuplicateEventIntegrityViolation;
        }

        // The session-scoped flag reports whether the atomic upsert applied a newer
        // lifecycle version or an authoritative active snapshot at the same version.
        // Initialize it to 1 for the insert path; the duplicate-key path overwrites it.
        await database.Database.ExecuteSqlRawAsync(
            "SET @teampulse_challenge_projection_applied = 1;",
            cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SET @teampulse_challenge_projection_is_snapshot = {(lifecycleEvent.EventType == "EmployeeActiveSnapshot" ? 1 : 0)};",
            cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_status_projections
                (employee_id, is_active, lifecycle_version, updated_at_utc)
            VALUES
                ({lifecycleEvent.EmployeeId}, {lifecycleEvent.IsActive},
                 {lifecycleEvent.LifecycleVersion}, {processedAtUtc}) AS incoming
            ON DUPLICATE KEY UPDATE
                is_active = IF(
                    (@teampulse_challenge_projection_applied := IF(
                        incoming.lifecycle_version > employee_status_projections.lifecycle_version OR
                        (incoming.lifecycle_version = employee_status_projections.lifecycle_version AND
                         @teampulse_challenge_projection_is_snapshot = 1), 1, 0)) = 1,
                    incoming.is_active, employee_status_projections.is_active),
                updated_at_utc = IF(
                    incoming.lifecycle_version > employee_status_projections.lifecycle_version OR
                    (incoming.lifecycle_version = employee_status_projections.lifecycle_version AND
                     @teampulse_challenge_projection_is_snapshot = 1),
                    incoming.updated_at_utc, employee_status_projections.updated_at_utc),
                lifecycle_version = GREATEST(employee_status_projections.lifecycle_version, incoming.lifecycle_version);
            """, cancellationToken);

        var projectionApplied = await database.Database.SqlQueryRaw<int>(
            "SELECT CAST(@teampulse_challenge_projection_applied AS SIGNED) AS Value")
            .SingleAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return projectionApplied == 1
            ? ProjectionProcessingOutcome.Applied
            : ProjectionProcessingOutcome.Stale;
    }

    public async Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.EmployeeStatusProjections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
    }
}
