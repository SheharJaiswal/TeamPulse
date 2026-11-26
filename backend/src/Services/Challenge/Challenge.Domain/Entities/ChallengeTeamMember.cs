using System;

namespace Challenge.Domain.Entities;

public sealed class ChallengeTeamMember
{
    private ChallengeTeamMember() { }

    public ChallengeTeamMember(Guid id, Guid challengeId, Guid teamId, Guid employeeId, DateTime assignedAtUtc)
    {
        if (id == Guid.Empty || challengeId == Guid.Empty || teamId == Guid.Empty || employeeId == Guid.Empty)
            throw new ArgumentException("Membership identifiers are required.");
        if (assignedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Assignment time must be UTC.", nameof(assignedAtUtc));
        Id = id;
        ChallengeId = challengeId;
        TeamId = teamId;
        EmployeeId = employeeId;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
}