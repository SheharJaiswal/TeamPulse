using System;

namespace Challenge.Domain.Entities;

public sealed class ChallengeTeam
{
    private ChallengeTeam() { }

    public ChallengeTeam(Guid id, Guid challengeId, string name, DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Team ID is required.", nameof(id));
        if (challengeId == Guid.Empty) throw new ArgumentException("Challenge ID is required.", nameof(challengeId));
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Team name must contain 1 to 100 characters.", nameof(name));
        if (createdAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Creation time must be UTC.", nameof(createdAtUtc));
        Id = id;
        ChallengeId = challengeId;
        Name = name.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}