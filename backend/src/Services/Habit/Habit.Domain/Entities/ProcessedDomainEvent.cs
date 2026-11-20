using System;

namespace Habit.Domain.Entities;

public sealed class ProcessedDomainEvent
{
    private ProcessedDomainEvent() { }

    public ProcessedDomainEvent(
        Guid eventId, string eventType, int schemaVersion, Guid employeeId,
        long lifecycleVersion, DateTime processedAtUtc)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(eventId));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type is required.", nameof(eventType));
        if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (lifecycleVersion < 1) throw new ArgumentOutOfRangeException(nameof(lifecycleVersion));
        if (processedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Processed timestamp must be UTC.", nameof(processedAtUtc));

        EventId = eventId;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        EmployeeId = employeeId;
        LifecycleVersion = lifecycleVersion;
        ProcessedAtUtc = processedAtUtc;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public Guid EmployeeId { get; private set; }
    public long LifecycleVersion { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }
}
