using System;

namespace Challenge.Domain.Entities;

public sealed class Challenge
{
    private Challenge() { }

    public Challenge(Guid id, string organizationId, Guid createdByEmployeeId, string name,
        string? description, DateOnly startDate, DateOnly endDate, DateTime createdAtUtc,
        DateOnly? registrationDeadline = null, string? goalInstructions = null,
        decimal goalTarget = 1m, string? goalUnit = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Challenge ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(organizationId)) throw new ArgumentException("Organization is required.", nameof(organizationId));
        if (createdByEmployeeId == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdByEmployeeId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new ArgumentException("Name must contain 1 to 120 characters.", nameof(name));
        if (description?.Length > 1000) throw new ArgumentException("Description cannot exceed 1,000 characters.", nameof(description));
        if (endDate < startDate) throw new ArgumentException("End date cannot be before start date.");
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Creation time must be UTC.", nameof(createdAtUtc));
        var instructions = string.IsNullOrWhiteSpace(goalInstructions) ? "Complete the challenge activity and record your progress." : goalInstructions.Trim();
        var unit = string.IsNullOrWhiteSpace(goalUnit) ? "completion" : goalUnit.Trim();
        if (instructions.Length > 500) throw new ArgumentException("Goal instructions cannot exceed 500 characters.", nameof(goalInstructions));
        if (goalTarget <= 0 || goalTarget > 1000000m) throw new ArgumentOutOfRangeException(nameof(goalTarget), "Goal target must be greater than zero and no more than 1,000,000.");
        if (unit.Length > 30) throw new ArgumentException("Goal unit cannot exceed 30 characters.", nameof(goalUnit));
        var deadline = registrationDeadline ?? startDate;
        if (deadline > startDate) throw new ArgumentException("Registration deadline cannot be after challenge start date.", nameof(registrationDeadline));

        Id = id;
        OrganizationId = organizationId.Trim();
        CreatedByEmployeeId = createdByEmployeeId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        RegistrationDeadline = deadline;
        GoalInstructions = instructions;
        GoalTarget = goalTarget;
        GoalUnit = unit;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string OrganizationId { get; private set; } = string.Empty;
    public Guid CreatedByEmployeeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateOnly RegistrationDeadline { get; private set; }
    public string GoalInstructions { get; private set; } = "Complete the challenge activity and record your progress.";
    public decimal GoalTarget { get; private set; } = 1m;
    public string GoalUnit { get; private set; } = "completion";
    public DateTime? RegistrationClosedAtUtc { get; private set; }
    public DateTime? TeamsFinalizedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public bool CloseRegistration(DateTime closedAtUtc)
    {
        if (closedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Closure time must be UTC.", nameof(closedAtUtc));
        if (RegistrationClosedAtUtc.HasValue) return false;
        RegistrationClosedAtUtc = closedAtUtc;
        return true;
    }

    public bool FinalizeTeams(DateTime finalizedAtUtc)
    {
        if (finalizedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Finalization time must be UTC.", nameof(finalizedAtUtc));
        if (TeamsFinalizedAtUtc.HasValue) return false;
        if (!RegistrationClosedAtUtc.HasValue) throw new InvalidOperationException("Registration must be closed before teams can be finalized.");
        TeamsFinalizedAtUtc = finalizedAtUtc;
        return true;
    }
}