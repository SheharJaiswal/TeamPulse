using System;

namespace EventTracking.Domain.Entities;

public sealed class ChallengeActivitySubmission
{
    private ChallengeActivitySubmission() { }

    public ChallengeActivitySubmission(Guid id, Guid employeeId, Guid challengeId, DateOnly activityDate,
        decimal quantity, Guid idempotencyKey, DateTime submittedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Submission ID is required.", nameof(id));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (challengeId == Guid.Empty) throw new ArgumentException("Challenge ID is required.", nameof(challengeId));
        if (quantity <= 0 || quantity > 1000000m || decimal.Round(quantity, 2) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero, no more than 1,000,000, and have at most two decimal places.");
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (submittedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Submission timestamp must be UTC.", nameof(submittedAtUtc));
        Id = id; EmployeeId = employeeId; ChallengeId = challengeId; ActivityDate = activityDate;
        Quantity = quantity; IdempotencyKey = idempotencyKey; SubmittedAtUtc = submittedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ChallengeId { get; private set; }
    public DateOnly ActivityDate { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
}
