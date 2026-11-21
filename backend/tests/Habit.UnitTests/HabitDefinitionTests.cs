using System;
using Habit.Domain.Entities;
using Xunit;

namespace Habit.UnitTests;

public sealed class HabitDefinitionTests
{
    private static HabitDefinition Create() => new(
        Guid.NewGuid(), Guid.NewGuid(), "  Drink water  ", "  Stay hydrated ", "  Wellness ",
        HabitFrequency.Daily, 8, new DateOnly(2026, 11, 21), null, DateTime.UtcNow);

    [Fact]
    public void Constructor_NormalizesText_AndStartsActive()
    {
        var habit = Create();
        Assert.Equal("Drink water", habit.Name);
        Assert.Equal("Stay hydrated", habit.Description);
        Assert.Equal("Wellness", habit.Category);
        Assert.Equal(HabitStatus.Active, habit.Status);
        Assert.Null(habit.ArchivedAtUtc);
    }

    [Fact]
    public void Constructor_RejectsInvalidTarget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HabitDefinition(
            Guid.NewGuid(), Guid.NewGuid(), "Read", null, null, HabitFrequency.Daily,
            0, new DateOnly(2026, 11, 21), null, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsEndDateBeforeStartDate()
    {
        Assert.Throws<ArgumentException>(() => new HabitDefinition(
            Guid.NewGuid(), Guid.NewGuid(), "Read", null, null, HabitFrequency.Daily,
            1, new DateOnly(2026, 11, 22), new DateOnly(2026, 11, 21), DateTime.UtcNow));
    }

    [Fact]
    public void Archive_IsIdempotent_AndPreservesFirstArchiveTimestamp()
    {
        var habit = Create();
        var archivedAt = DateTime.UtcNow.AddMinutes(1);
        habit.Archive(archivedAt);
        habit.Archive(archivedAt.AddMinutes(1));
        Assert.Equal(HabitStatus.Archived, habit.Status);
        Assert.Equal(archivedAt, habit.ArchivedAtUtc);
        Assert.Equal(archivedAt, habit.UpdatedAtUtc);
    }

    [Fact]
    public void Update_RejectsArchivedHabit()
    {
        var habit = Create();
        habit.Archive(DateTime.UtcNow.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => habit.Update(
            "Read", null, null, HabitFrequency.Daily, 1,
            new DateOnly(2026, 11, 21), null, DateTime.UtcNow.AddMinutes(2)));
    }
}
