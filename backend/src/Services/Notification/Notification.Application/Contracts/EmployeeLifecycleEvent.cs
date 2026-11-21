using System;
namespace Notification.Application.Contracts;
public sealed record EmployeeLifecycleEvent(Guid EventId, string EventType, int SchemaVersion, Guid EmployeeId, bool IsActive,
    long LifecycleVersion, Guid CorrelationId, DateTime OccurredAtUtc);