using System;

namespace EventTracking.Application.Contracts;

public sealed record CreateHabitActivityRequest(Guid HabitId, DateOnly ActivityDate, int Quantity, Guid IdempotencyKey);
public sealed record HabitActivityResponse(Guid Id, Guid HabitId, DateOnly ActivityDate, int Quantity, DateTime SubmittedAtUtc);
public sealed record HabitEligibilityResponse(Guid Id, int Status, DateOnly StartDate, DateOnly? EndDate);
