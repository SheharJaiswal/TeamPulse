using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace Identity.Infrastructure.Persistence;

public sealed class EmployeeInvitationActivationService(
    IdentityDbContext database,
    IInvitationTokenService tokenService,
    IPasswordHasher<Employee> passwordHasher,
    TimeProvider timeProvider) : IEmployeeInvitationActivationService
{
    public async Task<EmployeeInvitationActivationResult> ActivateAsync(
        ActivateEmployeeInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 12 ||
            request.Password.Length > 256)
            return Invalid();

        string digest;
        try
        {
            digest = tokenService.ComputeDigest(request.Token);
        }
        catch (ArgumentException)
        {
            return Invalid();
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var invitationId = await LockInvitationByDigestAsync(digest, cancellationToken);
        if (invitationId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Invalid();
        }

        var invitation = await database.EmployeeInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId.Value && item.TokenDigest == digest,
            cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (invitation is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Invalid();
        }

        if (!invitation.IsPendingAndUnexpired(nowUtc))
        {
            if (invitation.MarkExpired(nowUtc))
            {
                await RemoveDeliverySecretAsync(invitation.Id, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return Invalid();
        }

        var employeeId = Guid.NewGuid();
        var employeeForHash = new Employee(
            employeeId,
            invitation.Email,
            invitation.NormalizedEmail,
            invitation.DisplayName,
            "temporary",
            invitation.OrganizationId,
            invitation.TeamId,
            "Employee");
        var passwordHash = passwordHasher.HashPassword(employeeForHash, request.Password);
        var employee = new Employee(
            employeeId,
            invitation.Email,
            invitation.NormalizedEmail,
            invitation.DisplayName,
            passwordHash,
            invitation.OrganizationId,
            invitation.TeamId,
            "Employee");

        database.Employees.Add(employee);
        if (!invitation.Accept(nowUtc))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Invalid();
        }

        await RemoveDeliverySecretAsync(invitation.Id, cancellationToken);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new EmployeeInvitationActivationResult(EmployeeInvitationActivationStatus.Activated);
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            await transaction.RollbackAsync(cancellationToken);
            return Invalid();
        }
    }

    private async Task<Guid?> LockInvitationByDigestAsync(
        string digest,
        CancellationToken cancellationToken)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT Id FROM employee_invitations WHERE TokenDigest = @tokenDigest FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tokenDigest";
        parameter.Value = digest;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            null or DBNull => null,
            Guid id => id,
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            _ => Guid.Parse(Convert.ToString(result)!)
        };
    }

    private async Task RemoveDeliverySecretAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var secret = await database.InvitationDeliverySecrets.SingleOrDefaultAsync(
            item => item.InvitationId == invitationId, cancellationToken);
        if (secret is not null)
            database.InvitationDeliverySecrets.Remove(secret);
    }

    private static EmployeeInvitationActivationResult Invalid() =>
        new(EmployeeInvitationActivationStatus.Invalid);
}
