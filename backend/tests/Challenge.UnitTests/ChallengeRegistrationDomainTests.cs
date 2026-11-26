using System;
using Challenge.Domain.Entities;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeRegistrationDomainTests
{
    [Fact]
    public void Challenge_RegistrationCanCloseAndFinalizeOnlyAfterClosure()
    {
        var challenge = new Challenge.Domain.Entities.Challenge(
            Guid.NewGuid(), "org", Guid.NewGuid(), "Fitness", null,
            DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            DateTime.UtcNow);
        var now = DateTime.UtcNow;

        Assert.Throws<InvalidOperationException>(() => challenge.FinalizeTeams(now));
        Assert.True(challenge.CloseRegistration(now));
        Assert.False(challenge.CloseRegistration(now.AddMinutes(1)));
        Assert.True(challenge.FinalizeTeams(now.AddMinutes(2)));
        Assert.False(challenge.FinalizeTeams(now.AddMinutes(3)));
        Assert.Equal(now, challenge.RegistrationClosedAtUtc);
        Assert.Equal(now.AddMinutes(2), challenge.TeamsFinalizedAtUtc);
    }

    [Fact]
    public void Participation_CanAcknowledgeDeclineAndChangeBeforeClosure()
    {
        var respondedAt = DateTime.UtcNow;
        var participation = new ChallengeParticipation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), respondedAt, "Declined");

        Assert.Equal("Declined", participation.ResponseStatus);
        Assert.Null(participation.JoinedAtUtc);

        var joinedAt = respondedAt.AddMinutes(1);
        participation.ChangeResponse("Joined", joinedAt);

        Assert.Equal("Joined", participation.ResponseStatus);
        Assert.Equal(joinedAt, participation.JoinedAtUtc);
        Assert.Equal(joinedAt, participation.RespondedAtUtc);
    }

    [Fact]
    public void ChallengeTeam_RequiresNameAndValidIdentifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChallengeTeam(Guid.NewGuid(), Guid.NewGuid(), " ", DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() =>
            new ChallengeTeam(Guid.Empty, Guid.NewGuid(), "Team A", DateTime.UtcNow));
    }
}