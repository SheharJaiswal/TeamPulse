using System;
using EventTracking.Domain.Entities;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class HabitActivitySubmissionTests
{
    [Fact]
    public void Constructor_RejectsInvalidQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HabitActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 21), 0, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HabitActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 21), 1001, Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsMissingIdentityAndNonUtcTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new HabitActivitySubmission(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new DateOnly(2026, 11, 21), 1, Guid.NewGuid(), DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => new HabitActivitySubmission(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 21), 1, Guid.NewGuid(), DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Constructor_PreservesAppendOnlySubmissionValues()
    {
        var submittedAt = DateTime.UtcNow;
        var employeeId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var result = new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId,
            new DateOnly(2026, 11, 21), 3, key, submittedAt);
        Assert.Equal(employeeId, result.EmployeeId);
        Assert.Equal(habitId, result.HabitId);
        Assert.Equal(3, result.Quantity);
        Assert.Equal(key, result.IdempotencyKey);
        Assert.Equal(submittedAt, result.SubmittedAtUtc);
    }
}
