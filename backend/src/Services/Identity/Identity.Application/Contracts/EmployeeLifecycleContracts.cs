using System;

namespace Identity.Application.Contracts;

public sealed record EmployeeLifecycleRequest(string ReasonCode, string? Note);

public enum EmployeeLifecycleCommandStatus
{
    Transitioned,
    AlreadyInDesiredState,
    Replayed,
    NotFound,
    Conflict
}

public sealed record EmployeeLifecycleCommandResult(
    EmployeeLifecycleCommandStatus Status,
    Guid EmployeeId,
    bool IsActive,
    long LifecycleVersion,
    Guid? EventId,
    Guid CorrelationId);
