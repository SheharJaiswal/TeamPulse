using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Publishes an idempotent active-state snapshot for every active Identity employee.
/// Challenge consumes these snapshots to initialize or repair its local projection.
/// </summary>
public static class EmployeeLifecycleBackfill
{
    private const string Source = "hr-admin-api";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        IdentityDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        var activeEmployees = await database.Employees
            .FromSqlRaw("SELECT * FROM employees WHERE IsActive = 1 FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);

        foreach (var employee in activeEmployees)
            employee.InitializeActiveLifecycleVersion();

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var eventIds = activeEmployees
            .Select(employee => CreateSnapshotEventId(employee.Id, employee.LifecycleVersion))
            .ToArray();
        var existingEventIds = await database.OutboxMessages.AsNoTracking()
            .Where(message => eventIds.Contains(message.EventId))
            .Select(message => message.EventId)
            .ToListAsync(cancellationToken);
        var existing = existingEventIds.ToHashSet();

        foreach (var employee in activeEmployees)
        {
            var eventId = CreateSnapshotEventId(employee.Id, employee.LifecycleVersion);
            if (existing.Contains(eventId))
                continue;

            var envelope = new EmployeeLifecycleEventEnvelope(
                eventId,
                "EmployeeActiveSnapshot",
                1,
                nowUtc,
                eventId,
                new EmployeeLifecycleEventPayload(
                    employee.Id,
                    employee.Id,
                    Source,
                    "active",
                    "active",
                    employee.LifecycleVersion,
                    "identity-active-state-backfill",
                    null));

            database.OutboxMessages.Add(new OutboxMessage(
                eventId,
                "EmployeeActiveSnapshot",
                1,
                nowUtc,
                eventId,
                JsonSerializer.Serialize(envelope, JsonOptions)));
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Guid CreateSnapshotEventId(Guid employeeId, long lifecycleVersion)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"TeamPulse.Identity.EmployeeActiveSnapshot:{employeeId:D}:{lifecycleVersion}"));
        var eventId = new Guid(bytes.AsSpan(0, 16));
        return eventId == Guid.Empty ? new Guid(bytes.AsSpan(16, 16)) : eventId;
    }

    private sealed record EmployeeLifecycleEventEnvelope(
        Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
        Guid CorrelationId, EmployeeLifecycleEventPayload Payload);

    private sealed record EmployeeLifecycleEventPayload(
        Guid EmployeeId, Guid ActorEmployeeId, string Source, string PreviousStatus,
        string NewStatus, long LifecycleVersion, string ReasonCode, string? Note);
}
