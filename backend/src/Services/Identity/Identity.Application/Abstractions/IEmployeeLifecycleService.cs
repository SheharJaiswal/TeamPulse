using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Contracts;

namespace Identity.Application.Abstractions;

public interface IEmployeeLifecycleService
{
    Task<EmployeeLifecycleCommandResult> ExecuteAsync(
        Guid employeeId, Guid actorEmployeeId, bool desiredIsActive,
        string idempotencyKey, string reasonCode, string? note,
        Guid correlationId, CancellationToken cancellationToken);
}
