using System;
using Habit.Domain.Entities;

namespace Habit.Application.Contracts;

public sealed record CreateHabitRequest(
    string Name,
    string? Description,
    string? Category,
    HabitFrequency Frequency,
    int TargetCount,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record UpdateHabitRequest(
    string Name,
    string? Description,
    string? Category,
    HabitFrequency Frequency,
    int TargetCount,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record HabitResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    HabitFrequency Frequency,
    int TargetCount,
    DateOnly StartDate,
    DateOnly? EndDate,
    HabitStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ArchivedAtUtc);
