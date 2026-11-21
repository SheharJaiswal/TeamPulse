using System;
namespace EventTracking.Domain.Entities;
public sealed class EmployeeStatusProjection
{
    private EmployeeStatusProjection() { }
    public EmployeeStatusProjection(Guid employeeId, bool isActive, long lifecycleVersion, DateTime updatedAtUtc)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (lifecycleVersion < 0) throw new ArgumentOutOfRangeException(nameof(lifecycleVersion));
        if (updatedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Projection timestamp must be UTC.", nameof(updatedAtUtc));
        EmployeeId = employeeId; IsActive = isActive; LifecycleVersion = lifecycleVersion; UpdatedAtUtc = updatedAtUtc;
    }
    public Guid EmployeeId { get; private set; }
    public bool IsActive { get; private set; }
    public long LifecycleVersion { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}