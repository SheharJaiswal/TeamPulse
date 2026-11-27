using System;
using System.Collections.Generic;

namespace EventTracking.Application.Contracts;

public sealed record CreateChallengeActivityRequest(Guid ChallengeId, DateOnly ActivityDate, decimal Quantity, Guid IdempotencyKey, int TimeZoneOffsetMinutes = 0);
public sealed record ChallengeActivityResponse(Guid Id, Guid ChallengeId, Guid EmployeeId, DateOnly ActivityDate, decimal Quantity, DateTime SubmittedAtUtc);
public sealed record ChallengeActivityProgressResponse(Guid ChallengeId, decimal TotalQuantity, IReadOnlyList<ChallengeActivityResponse> Entries);
public sealed record ChallengeActivityGoalResponse(Guid ChallengeId, string GoalInstructions, decimal GoalTarget, string GoalUnit, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<Guid>? TeamMemberEmployeeIds = null);
public sealed record ChallengeActivityRecordedEvent(Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc, Guid CorrelationId, ChallengeActivityRecordedPayload Payload);
public sealed record ChallengeActivityRecordedPayload(Guid EmployeeId, Guid SubmissionId, Guid ChallengeId, DateOnly ActivityDate, decimal Quantity);
