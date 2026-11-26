using System;

namespace Challenge.Domain.Entities;

public sealed class ChallengeParticipation
{
    private ChallengeParticipation() { }

    public ChallengeParticipation(Guid id, Guid challengeId, Guid employeeId, DateTime respondedAtUtc,
        string responseStatus = "Joined")
    {
        if (id == Guid.Empty) throw new ArgumentException("Participation ID is required.", nameof(id));
        if (challengeId == Guid.Empty) throw new ArgumentException("Challenge ID is required.", nameof(challengeId));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (respondedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Response time must be UTC.", nameof(respondedAtUtc));
        if (responseStatus is not ("Joined" or "Declined")) throw new ArgumentException("Response must be Joined or Declined.", nameof(responseStatus));

        Id = id;
        ChallengeId = challengeId;
        EmployeeId = employeeId;
        ResponseStatus = responseStatus;
        RespondedAtUtc = respondedAtUtc;
        JoinedAtUtc = responseStatus == "Joined" ? respondedAtUtc : null;
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string ResponseStatus { get; private set; } = "Joined";
    public DateTime RespondedAtUtc { get; private set; }
    public DateTime? JoinedAtUtc { get; private set; }
}