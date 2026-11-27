using System;
using Challenge.Domain.Entities;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeGoalDomainTests
{
    [Fact]
    public void Constructor_PersistsGoalInstructionsTargetAndUnit()
    {
        var challenge = new Challenge.Domain.Entities.Challenge(Guid.NewGuid(), "org", Guid.NewGuid(), "Walking", null,
            new DateOnly(2026, 11, 28), new DateOnly(2026, 12, 4), DateTime.UtcNow,
            new DateOnly(2026, 11, 27), "Walk every day and record your steps.", 35000m, "steps");
        Assert.Equal("Walk every day and record your steps.", challenge.GoalInstructions);
        Assert.Equal(35000m, challenge.GoalTarget);
        Assert.Equal("steps", challenge.GoalUnit);
    }

    [Fact]
    public void Constructor_RejectsInvalidGoalTarget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Challenge(Guid.NewGuid(), "org", Guid.NewGuid(),
            "Walking", null, new DateOnly(2026, 11, 28), new DateOnly(2026, 12, 4), DateTime.UtcNow,
            new DateOnly(2026, 11, 27), "Walk every day.", 0m, "steps"));
    }
}
