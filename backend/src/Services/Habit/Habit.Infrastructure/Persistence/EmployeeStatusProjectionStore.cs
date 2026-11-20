using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Application.Contracts;
using Habit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Habit.Infrastructure.Persistence;

public sealed class EmployeeStatusProjectionStore(
    IDbContextFactory<HabitDbContext> contextFactory) : IEmployeeStatusProjectionStore
{
    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        EmployeeLifecycleEvent lifecycleEvent,
        DateTime processedAtUtc,
        CancellationToken cancellationToken)
    {
        if (processedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Processing timestamp must be UTC.", nameof(processedAtUtc));

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        database.ProcessedDomainEvents.Add(new ProcessedDomainEvent(
            lifecycleEvent.EventId,
            lifecycleEvent.EventType,
            lifecycleEvent.SchemaVersion,
            lifecycleEvent.EmployeeId,
            lifecycleEvent.LifecycleVersion,
            processedAtUtc));

        try
        {
            // Save the unique inbox key first. A duplicate-key conflict here can only be
            // the eventId primary key because this is the only write before the projection upsert.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is MySqlException mysqlException &&
            mysqlException.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProjectionProcessingOutcome.Duplicate;
        }

        var previousVersion = await database.EmployeeStatusProjections
            .AsNoTracking()
            .Where(x => x.EmployeeId == lifecycleEvent.EmployeeId)
            .Select(x => (long?)x.LifecycleVersion)
            .SingleOrDefaultAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO employee_status_projections
                (employee_id, is_active, lifecycle_version, updated_at_utc)
            VALUES
                ({lifecycleEvent.EmployeeId}, {lifecycleEvent.IsActive},
                 {lifecycleEvent.LifecycleVersion}, {processedAtUtc}) AS incoming
            ON DUPLICATE KEY UPDATE
                is_active = IF(
                    incoming.lifecycle_version > employee_status_projections.lifecycle_version,
                    incoming.is_active, employee_status_projections.is_active),
                updated_at_utc = IF(
                    incoming.lifecycle_version > employee_status_projections.lifecycle_version,
                    incoming.updated_at_utc, employee_status_projections.updated_at_utc),
                lifecycle_version = GREATEST(
                    employee_status_projections.lifecycle_version, incoming.lifecycle_version);
            """, cancellationToken);

        var finalVersion = await database.EmployeeStatusProjections
            .AsNoTracking()
            .Where(x => x.EmployeeId == lifecycleEvent.EmployeeId)
            .Select(x => x.LifecycleVersion)
            .SingleAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Do not depend on provider-specific affected-row semantics for ON DUPLICATE KEY UPDATE.
        // A concurrent higher version may win while this transaction waits for the row lock.
        var applied = (!previousVersion.HasValue || lifecycleEvent.LifecycleVersion > previousVersion.Value)
            && finalVersion == lifecycleEvent.LifecycleVersion;

        return applied ? ProjectionProcessingOutcome.Applied : ProjectionProcessingOutcome.Stale;
    }

    public async Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(
        Guid employeeId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.EmployeeStatusProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
    }
}
