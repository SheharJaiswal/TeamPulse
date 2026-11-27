using System;
using System.Linq;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Infrastructure.Persistence;

public sealed class EmployeeInvitationService(
    IdentityDbContext database,
    IInvitationTokenService tokenService,
    TimeProvider timeProvider) : IEmployeeInvitationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(24);

    public async Task<EmployeeInvitationCommandResult> CreateAsync(
        Guid invitedByEmployeeId,
        CreateEmployeeInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (invitedByEmployeeId == Guid.Empty)
            throw new ArgumentException("Inviting employee ID is required.", nameof(invitedByEmployeeId));

        var email = RequiredTrimmed(request.Email, nameof(request.Email), 320);
        var displayName = RequiredTrimmed(request.DisplayName, nameof(request.DisplayName), 150);
        var organizationId = RequiredTrimmed(request.OrganizationId, nameof(request.OrganizationId), 100);
        var teamId = OptionalTrimmed(request.TeamId, nameof(request.TeamId), 100);
        var normalizedEmail = email.ToUpperInvariant();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var employeeExists = await database.Employees.AnyAsync(
            employee => employee.NormalizedEmail == normalizedEmail, cancellationToken);
        var pendingInvitation = await database.EmployeeInvitations.SingleOrDefaultAsync(
            invitation => invitation.PendingNormalizedEmail == normalizedEmail, cancellationToken);
        if (employeeExists ||
            (pendingInvitation is not null && pendingInvitation.IsPendingAndUnexpired(nowUtc)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(EmployeeInvitationCommandStatus.Conflict, null);
        }

        if (pendingInvitation is not null && pendingInvitation.MarkExpired(nowUtc))
            await RemoveDeliverySecretAsync(pendingInvitation.Id, cancellationToken);

        var material = tokenService.Create();
        var invitation = new EmployeeInvitation(
            Guid.NewGuid(), email, normalizedEmail, displayName,
            organizationId, teamId, material.TokenDigest,
            invitedByEmployeeId, nowUtc, nowUtc.Add(InvitationLifetime));

        database.EmployeeInvitations.Add(invitation);
        database.InvitationDeliverySecrets.Add(new InvitationDeliverySecret(
            Guid.NewGuid(), invitation.Id, invitation.DeliveryGeneration,
            material.ProtectedToken, nowUtc));
        AddDeliveryOutboxMessage(invitation, nowUtc);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EmployeeInvitationCommandResult(
            EmployeeInvitationCommandStatus.Created, ToSummary(invitation));
    }

    public async Task<EmployeeInvitationSummary?> GetAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = await database.EmployeeInvitations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        return invitation is null ? null : ToSummary(invitation);
    }

    public async Task<EmployeeInvitationCommandResult> ResendAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        if (!await LockInvitationAsync(invitationId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(EmployeeInvitationCommandStatus.NotFound, null);
        }

        var invitation = await database.EmployeeInvitations.SingleAsync(
            item => item.Id == invitationId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (!invitation.IsPendingAndUnexpired(nowUtc))
        {
            if (invitation.Status == "Pending" && invitation.MarkExpired(nowUtc))
            {
                await RemoveDeliverySecretAsync(invitationId, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new EmployeeInvitationCommandResult(
                    EmployeeInvitationCommandStatus.Expired, ToSummary(invitation));
            }

            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(
                EmployeeInvitationCommandStatus.AlreadyTerminal, ToSummary(invitation));
        }

        var material = tokenService.Create();
        invitation.RotateToken(material.TokenDigest, nowUtc, nowUtc.Add(InvitationLifetime));
        await ReplaceDeliverySecretAsync(invitation, material.ProtectedToken, nowUtc, cancellationToken);
        AddDeliveryOutboxMessage(invitation, nowUtc);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EmployeeInvitationCommandResult(
            EmployeeInvitationCommandStatus.Updated, ToSummary(invitation));
    }

    public async Task<EmployeeInvitationCommandResult> CancelAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        if (!await LockInvitationAsync(invitationId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(EmployeeInvitationCommandStatus.NotFound, null);
        }

        var invitation = await database.EmployeeInvitations.SingleAsync(
            item => item.Id == invitationId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (invitation.Status == "Pending" && invitation.ExpiresAtUtc <= nowUtc)
        {
            invitation.MarkExpired(nowUtc);
            await RemoveDeliverySecretAsync(invitationId, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(
                EmployeeInvitationCommandStatus.Expired, ToSummary(invitation));
        }

        if (!invitation.Cancel(nowUtc))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(
                EmployeeInvitationCommandStatus.AlreadyTerminal, ToSummary(invitation));
        }

        await RemoveDeliverySecretAsync(invitationId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EmployeeInvitationCommandResult(
            EmployeeInvitationCommandStatus.Updated, ToSummary(invitation));
    }

    private async Task<bool> LockInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT Id FROM employee_invitations WHERE Id = @invitationId FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@invitationId";
        parameter.Value = invitationId;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task ReplaceDeliverySecretAsync(
        EmployeeInvitation invitation,
        string protectedToken,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        // Reset the delivery lease atomically. The worker may have acquired or changed
        // the lease since this request began; updating by invitation ID avoids a stale
        // tracked LeaseToken causing an optimistic-concurrency failure.
        var updated = await database.InvitationDeliverySecrets
            .Where(secret => secret.InvitationId == invitation.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(secret => secret.Generation, invitation.DeliveryGeneration)
                .SetProperty(secret => secret.ProtectedToken, protectedToken)
                .SetProperty(secret => secret.CreatedAtUtc, createdAtUtc)
                .SetProperty(secret => secret.AttemptCount, 0)
                .SetProperty(secret => secret.NextAttemptAtUtc, (DateTime?)null)
                .SetProperty(secret => secret.LeaseToken, (Guid?)null)
                .SetProperty(secret => secret.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(secret => secret.LastErrorCode, (string?)null),
                cancellationToken);

        if (updated == 0)
        {
            database.InvitationDeliverySecrets.Add(new InvitationDeliverySecret(
                Guid.NewGuid(), invitation.Id, invitation.DeliveryGeneration,
                protectedToken, createdAtUtc));
        }
    }

    private async Task RemoveDeliverySecretAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        // Cancellation/expiry must revoke the delivery secret even if a delivery worker
        // concurrently changed its lease token. ExecuteDelete avoids stale EF concurrency
        // tokens and performs the deletion atomically by invitation ID.
        await database.InvitationDeliverySecrets
            .Where(secret => secret.InvitationId == invitationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void AddDeliveryOutboxMessage(EmployeeInvitation invitation, DateTime occurredAtUtc)
    {
        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new InvitationDeliveryRequested(
            invitation.Id, invitation.DeliveryGeneration), JsonOptions);
        database.OutboxMessages.Add(new OutboxMessage(
            eventId, "EmployeeInvitationDeliveryRequested", 1,
            occurredAtUtc, eventId, payload));
    }

    private static EmployeeInvitationSummary ToSummary(EmployeeInvitation invitation) =>
        new(invitation.Id, invitation.Email, invitation.DisplayName,
            invitation.OrganizationId, invitation.TeamId, invitation.Status,
            invitation.CreatedAtUtc, invitation.ExpiresAtUtc, invitation.DeliveryGeneration);

    private static string RequiredTrimmed(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
            throw new ArgumentException($"A value between 1 and {maxLength} characters is required.", name);
        return normalized;
    }

    private static string? OptionalTrimmed(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return null;
        if (normalized.Length > maxLength)
            throw new ArgumentException($"A value cannot exceed {maxLength} characters.", name);
        return normalized;
    }

    private sealed record InvitationDeliveryRequested(Guid InvitationId, int Generation);
}
