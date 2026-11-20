using System;

namespace Challenge.Application.Contracts;

public sealed record EmployeeLifecycleEvent(
    Guid EventId, string EventType, int SchemaVersion, Guid EmployeeId, bool IsActive, long LifecycleVersion,
    Guid CorrelationId, DateTime OccurredAtUtc, Guid ActorEmployeeId, string Source, string PreviousStatus,
    string NewStatus, string ReasonCode, string FingerprintAlgorithm, string ContentFingerprint);
