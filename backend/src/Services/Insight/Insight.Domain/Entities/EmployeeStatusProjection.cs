namespace Insight.Domain.Entities;

public sealed class EmployeeStatusProjection
{
    public EmployeeStatusProjection(Guid employeeId, bool isActive, long lifecycleVersion, DateTime updatedAtUtc)
    {
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (lifecycleVersion <= 0) throw new ArgumentOutOfRangeException(nameof(lifecycleVersion));
        if (updatedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Projection timestamp must be UTC.", nameof(updatedAtUtc));
        EmployeeId = employeeId; IsActive = isActive; LifecycleVersion = lifecycleVersion; UpdatedAtUtc = updatedAtUtc;
    }
    public Guid EmployeeId { get; }
    public bool IsActive { get; }
    public long LifecycleVersion { get; }
    public DateTime UpdatedAtUtc { get; }
}