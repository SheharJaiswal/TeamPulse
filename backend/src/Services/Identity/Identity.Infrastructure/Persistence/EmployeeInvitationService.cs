using System;
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
        var teamId = RequiredTrimmed(request.TeamId, nameof(request.TeamId), 100);
        var normalizedEmail = email.ToUpperInvariant();
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var employeeExists = await database.Employees.AnyAsync(
            employee => employee.NormalizedEmail == normalizedEmail, cancellationToken);
        var invitationExists = await database.EmployeeInvitations.AnyAsync(
            invitation => invitation.PendingNormalizedEmail == normalizedEmail, cancellationToken);
        if (employeeExists || invitationExists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new EmployeeInvitationCommandResult(EmployeeInvitationCommandStatus.Conflict, null);
        }

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
        var existing = await database.InvitationDeliverySecrets.SingleOrDefaultAsync(
            secret => secret.InvitationId == invitation.Id, cancellationToken);
        if (existing is null)
        {
            database.InvitationDeliverySecrets.Add(new InvitationDeliverySecret(
                Guid.NewGuid(), invitation.Id, invitation.DeliveryGeneration,
                protectedToken, createdAtUtc));
            return;
        }

        existing.Replace(invitation.DeliveryGeneration, protectedToken, createdAtUtc);
    }

    private async Task RemoveDeliverySecretAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var existing = await database.InvitationDeliverySecrets.SingleOrDefaultAsync(
            secret => secret.InvitationId == invitationId, cancellationToken);
        if (existing is not null)
            database.InvitationDeliverySecrets.Remove(existing);
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

    private sealed record InvitationDeliveryRequested(Guid InvitationId, int Generation);
}
