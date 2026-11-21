using System;

namespace EventTracking.Domain.Entities;

public sealed class HabitActivitySubmission
{
    private HabitActivitySubmission() { }

    public HabitActivitySubmission(Guid id, Guid employeeId, Guid habitId, DateOnly activityDate,
        int quantity, Guid idempotencyKey, DateTime submittedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Submission ID is required.", nameof(id));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (habitId == Guid.Empty) throw new ArgumentException("Habit ID is required.", nameof(habitId));
        if (quantity is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be between 1 and 1,000.");
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (submittedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Submission timestamp must be UTC.", nameof(submittedAtUtc));
        Id = id; EmployeeId = employeeId; HabitId = habitId; ActivityDate = activityDate;
        Quantity = quantity; IdempotencyKey = idempotencyKey; SubmittedAtUtc = submittedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid HabitId { get; private set; }
    public DateOnly ActivityDate { get; private set; }
    public int Quantity { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
}
