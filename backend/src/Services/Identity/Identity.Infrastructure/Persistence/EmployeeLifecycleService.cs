using System;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Infrastructure.Persistence;

public sealed class EmployeeLifecycleService(IdentityDbContext database) : IEmployeeLifecycleService
{
    private const string Source = "hr-admin-api";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EmployeeLifecycleCommandResult> ExecuteAsync(
        Guid employeeId, Guid actorEmployeeId, bool desiredIsActive,
        string idempotencyKey, string reasonCode, string? note,
        Guid correlationId, CancellationToken cancellationToken)
    {
        var normalizedReason = reasonCode.Trim();
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var fingerprint = CreateFingerprint(employeeId, desiredIsActive, normalizedReason, normalizedNote);

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var existing = await FindByIdempotencyKeyAsync(actorEmployeeId, idempotencyKey, cancellationToken);
        if (existing is not null)
            return ReplayOrConflict(existing, fingerprint);

        var employee = await database.Employees.SingleOrDefaultAsync(
            candidate => candidate.Id == employeeId, cancellationToken);

        if (employee is null)
            return new EmployeeLifecycleCommandResult(
                EmployeeLifecycleCommandStatus.NotFound, employeeId, false, 0, null, correlationId);

        var observedVersion = employee.LifecycleVersion;
        var lockedVersion = await LockEmployeeAndReadVersionAsync(employeeId, cancellationToken);
        if (lockedVersion is null)
            return new EmployeeLifecycleCommandResult(
                EmployeeLifecycleCommandStatus.NotFound, employeeId, false, 0, null, correlationId);

        if (lockedVersion.Value != observedVersion)
        {
            existing = await FindByIdempotencyKeyAsync(actorEmployeeId, idempotencyKey, cancellationToken);
            if (existing is not null)
                return ReplayOrConflict(existing, fingerprint);

            return new EmployeeLifecycleCommandResult(
                EmployeeLifecycleCommandStatus.Conflict,
                employeeId, false, lockedVersion.Value, null, correlationId);
        }

        if (!employee.ChangeActiveState(desiredIsActive))
        {
            existing = await FindByIdempotencyKeyAsync(actorEmployeeId, idempotencyKey, cancellationToken);
            if (existing is not null)
                return ReplayOrConflict(existing, fingerprint);

            return new EmployeeLifecycleCommandResult(
                EmployeeLifecycleCommandStatus.AlreadyInDesiredState,
                employee.Id, employee.IsActive, employee.LifecycleVersion, null, correlationId);
        }

        var occurredAtUtc = DateTime.UtcNow;
        var eventId = Guid.NewGuid();
        var previousStatus = desiredIsActive ? "inactive" : "active";
        var newStatus = desiredIsActive ? "active" : "inactive";
        var eventType = desiredIsActive ? "EmployeeReactivated" : "EmployeeDeactivated";
        var envelope = new EmployeeLifecycleEventEnvelope(
            eventId, eventType, 1, occurredAtUtc, correlationId,
            new EmployeeLifecycleEventPayload(
                employee.Id, actorEmployeeId, Source, previousStatus, newStatus,
                employee.LifecycleVersion, normalizedReason, normalizedNote));

        database.EmployeeLifecycleAudits.Add(new EmployeeLifecycleAudit(
            eventId, employee.Id, actorEmployeeId, Source, previousStatus, newStatus,
            employee.LifecycleVersion, occurredAtUtc, normalizedReason, normalizedNote,
            correlationId, idempotencyKey, fingerprint));

        database.OutboxMessages.Add(new OutboxMessage(
            eventId, eventType, 1, occurredAtUtc, correlationId,
            JsonSerializer.Serialize(envelope, JsonOptions)));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new EmployeeLifecycleCommandResult(
                EmployeeLifecycleCommandStatus.Transitioned, employee.Id, employee.IsActive,
                employee.LifecycleVersion, eventId, correlationId);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            return await ResolveRaceAsync(
                actorEmployeeId, idempotencyKey, fingerprint, employeeId, correlationId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            var committed = await FindByIdempotencyKeyAsync(actorEmployeeId, idempotencyKey, cancellationToken);
            if (committed is not null)
                return ReplayOrConflict(committed, fingerprint);

            throw;
        }
    }

    private async Task<long?> LockEmployeeAndReadVersionAsync(
        Guid employeeId, CancellationToken cancellationToken)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT LifecycleVersion FROM employees WHERE Id = @employeeId FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@employeeId";
        parameter.Value = employeeId;
        command.Parameters.Add(parameter);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private Task<EmployeeLifecycleAudit?> FindByIdempotencyKeyAsync(
        Guid actorEmployeeId, string idempotencyKey, CancellationToken cancellationToken) =>
        database.EmployeeLifecycleAudits.AsNoTracking().SingleOrDefaultAsync(
            audit => audit.ActorEmployeeId == actorEmployeeId && audit.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private async Task<EmployeeLifecycleCommandResult> ResolveRaceAsync(
        Guid actorEmployeeId, string idempotencyKey, string fingerprint,
        Guid employeeId, Guid correlationId, CancellationToken cancellationToken)
    {
        var committed = await FindByIdempotencyKeyAsync(actorEmployeeId, idempotencyKey, cancellationToken);
        if (committed is not null)
            return ReplayOrConflict(committed, fingerprint);

        return new EmployeeLifecycleCommandResult(
            EmployeeLifecycleCommandStatus.Conflict, employeeId, false, 0, null, correlationId);
    }

    private static EmployeeLifecycleCommandResult ReplayOrConflict(
        EmployeeLifecycleAudit audit, string fingerprint)
    {
        var stored = Convert.FromHexString(audit.RequestFingerprint);
        var supplied = Convert.FromHexString(fingerprint);
        var matches = stored.Length == supplied.Length &&
                      CryptographicOperations.FixedTimeEquals(stored, supplied);

        return new EmployeeLifecycleCommandResult(
            matches ? EmployeeLifecycleCommandStatus.Replayed : EmployeeLifecycleCommandStatus.Conflict,
            audit.EmployeeId, audit.NewStatus == "active", audit.LifecycleVersion,
            audit.Id, audit.CorrelationId);
    }

    private static string CreateFingerprint(
        Guid employeeId, bool desiredIsActive, string reasonCode, string? note)
    {
        var canonical = string.Join("\n",
            employeeId.ToString("D"), desiredIsActive ? "active" : "inactive",
            reasonCode, note ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record EmployeeLifecycleEventEnvelope(
        Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
        Guid CorrelationId, EmployeeLifecycleEventPayload Payload);

    private sealed record EmployeeLifecycleEventPayload(
        Guid EmployeeId, Guid ActorEmployeeId, string Source, string PreviousStatus,
        string NewStatus, long LifecycleVersion, string ReasonCode, string? Note);
}
