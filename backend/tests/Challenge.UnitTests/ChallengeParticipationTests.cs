using System;
using Challenge.Domain.Entities;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeParticipationTests
{
    [Fact]
    public void Constructor_StoresParticipationForEmployeeAndChallenge()
    {
        var id = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var joinedAtUtc = DateTime.UtcNow;

        var participation = new ChallengeParticipation(id, challengeId, employeeId, joinedAtUtc);

        Assert.Equal(id, participation.Id);
        Assert.Equal(challengeId, participation.ChallengeId);
        Assert.Equal(employeeId, participation.EmployeeId);
        Assert.Equal(joinedAtUtc, participation.JoinedAtUtc);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("challenge")]
    [InlineData("employee")]
    public void Constructor_RejectsEmptyIdentifiers(string identifier)
    {
        var id = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        if (identifier == "id") id = Guid.Empty;
        if (identifier == "challenge") challengeId = Guid.Empty;
        if (identifier == "employee") employeeId = Guid.Empty;

        Assert.Throws<ArgumentException>(() =>
            new ChallengeParticipation(id, challengeId, employeeId, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsNonUtcJoinTimestamp()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChallengeParticipation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Now));
    }
}
