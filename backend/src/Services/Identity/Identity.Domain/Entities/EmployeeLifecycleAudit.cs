using System;

namespace Identity.Domain.Entities;

public sealed class EmployeeLifecycleAudit
{
    private EmployeeLifecycleAudit() { }

    public EmployeeLifecycleAudit(
        Guid id, Guid employeeId, Guid actorEmployeeId, string source,
        string previousStatus, string newStatus, long lifecycleVersion,
        DateTime occurredAtUtc, string reasonCode, string? note,
        Guid correlationId, string idempotencyKey, string requestFingerprint)
    {
        Id = id;
        EmployeeId = employeeId;
        ActorEmployeeId = actorEmployeeId;
        Source = source;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        LifecycleVersion = lifecycleVersion;
        OccurredAtUtc = occurredAtUtc;
        ReasonCode = reasonCode;
        Note = note;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ActorEmployeeId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string PreviousStatus { get; private set; } = string.Empty;
    public string NewStatus { get; private set; } = string.Empty;
    public long LifecycleVersion { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
}
