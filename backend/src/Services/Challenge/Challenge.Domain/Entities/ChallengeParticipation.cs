using System;

namespace Challenge.Domain.Entities;

public sealed class ChallengeParticipation
{
    private ChallengeParticipation() { }

    public ChallengeParticipation(Guid id, Guid challengeId, Guid employeeId, DateTime joinedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Participation ID is required.", nameof(id));
        if (challengeId == Guid.Empty) throw new ArgumentException("Challenge ID is required.", nameof(challengeId));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (joinedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Join time must be UTC.", nameof(joinedAtUtc));

        Id = id;
        ChallengeId = challengeId;
        EmployeeId = employeeId;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
}
