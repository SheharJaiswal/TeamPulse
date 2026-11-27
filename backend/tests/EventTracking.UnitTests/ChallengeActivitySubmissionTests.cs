using System;
using EventTracking.Domain.Entities;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class ChallengeActivitySubmissionTests
{
    [Fact]
    public void Constructor_RejectsInvalidQuantityAndIdentity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 27), 0m, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 27), 1000000.01m, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new DateOnly(2026, 11, 27), 1m, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 27), 1m, Guid.Empty, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsMoreThanTwoDecimalPlacesAndNonUtcTimestamp()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 27), 1.234m, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new ChallengeActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 27), 1m, Guid.NewGuid(),
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Constructor_PreservesActivityValues()
    {
        var id = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var challenge = Guid.NewGuid();
        var key = Guid.NewGuid();
        var submitted = DateTime.UtcNow;
        var value = new ChallengeActivitySubmission(id, employee, challenge, new DateOnly(2026, 11, 27), 2.5m, key, submitted);
        Assert.Equal(id, value.Id);
        Assert.Equal(employee, value.EmployeeId);
        Assert.Equal(challenge, value.ChallengeId);
        Assert.Equal(2.5m, value.Quantity);
        Assert.Equal(key, value.IdempotencyKey);
        Assert.Equal(submitted, value.SubmittedAtUtc);
    }
}
