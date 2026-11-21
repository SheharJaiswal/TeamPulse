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

        // UseAffectedRows=true makes the upsert report 1 for insert, 2 for a changed
        // row and 0 when an equal/lower lifecycle version leaves the projection unchanged.
        var affectedRows = await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_status_projections
                (employee_id, is_active, lifecycle_version, updated_at_utc)
            VALUES
                ({lifecycleEvent.EmployeeId}, {lifecycleEvent.IsActive},
                 {lifecycleEvent.LifecycleVersion}, {processedAtUtc}) AS incoming
            ON DUPLICATE KEY UPDATE
                is_active = IF(incoming.lifecycle_version > employee_status_projections.lifecycle_version,
                    incoming.is_active, employee_status_projections.is_active),
                updated_at_utc = IF(incoming.lifecycle_version > employee_status_projections.lifecycle_version,
                    incoming.updated_at_utc, employee_status_projections.updated_at_utc),
                lifecycle_version = GREATEST(employee_status_projections.lifecycle_version, incoming.lifecycle_version);
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return affectedRows > 0 ? ProjectionProcessingOutcome.Applied : ProjectionProcessingOutcome.Stale;
    }

    public async Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.EmployeeStatusProjections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
    }
}
