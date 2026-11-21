namespace Insight.Application.Contracts;

public sealed record EmployeeLifecycleEvent(Guid EventId, string EventType, int SchemaVersion, Guid EmployeeId, bool IsActive, long LifecycleVersion, Guid CorrelationId, DateTime OccurredAtUtc);