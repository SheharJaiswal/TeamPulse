using System.Data;
using System.Data.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.AdminBootstrap;

public enum BootstrapResult
{
    Created,
    AdministratorAlreadyExists,
    EmailAlreadyExists,
    LockUnavailable
}

public sealed class HrAdminBootstrapper(
    IdentityDbContext database,
    IPasswordHasher<Employee> passwordHasher)
{
    public const string HrAdminRole = "HrAdmin";
    private const string LockName = "teampulse_identity_hr_admin_bootstrap";

    public async Task<BootstrapResult> RunAsync(
        BootstrapOptions options,
        CancellationToken cancellationToken = default)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        var lockAcquired = false;

        try
        {
            lockAcquired = await AcquireLockAsync(cancellationToken);
            if (!lockAcquired)
                return BootstrapResult.LockUnavailable;

            await using var transaction =
                await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            if (await database.Employees.AnyAsync(
                    employee => employee.Role == HrAdminRole,
                    cancellationToken))
            {
                return BootstrapResult.AdministratorAlreadyExists;
            }

            var normalizedEmail = options.Email.Trim().ToUpperInvariant();
            if (await database.Employees.AnyAsync(
                    employee => employee.NormalizedEmail == normalizedEmail,
                    cancellationToken))
            {
                return BootstrapResult.EmailAlreadyExists;
            }

            var id = Guid.NewGuid();
            var employeeForHash = new Employee(
                id, options.Email.Trim(), normalizedEmail, options.DisplayName.Trim(),
                "temporary", options.OrganizationId.Trim(), options.TeamId.Trim(), HrAdminRole);
            var passwordHash = passwordHasher.HashPassword(employeeForHash, options.Password);
            var employee = new Employee(
                id, options.Email.Trim(), normalizedEmail, options.DisplayName.Trim(),
                passwordHash, options.OrganizationId.Trim(), options.TeamId.Trim(), HrAdminRole);

            database.Employees.Add(employee);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return BootstrapResult.Created;
        }
        finally
        {
            try
            {
                if (lockAcquired)
                    await ReleaseLockAsync(CancellationToken.None);
            }
            finally
            {
                await database.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<bool> AcquireLockAsync(CancellationToken cancellationToken)
    {
        await using var command = CreateCommand("SELECT GET_LOCK(@lockName, @timeoutSeconds)");
        AddParameter(command, "@lockName", LockName);
        AddParameter(command, "@timeoutSeconds", 10);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private async Task ReleaseLockAsync(CancellationToken cancellationToken)
    {
        await using var command = CreateCommand("SELECT RELEASE_LOCK(@lockName)");
        AddParameter(command, "@lockName", LockName);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private DbCommand CreateCommand(string commandText)
    {
        var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
