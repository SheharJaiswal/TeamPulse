using System;

namespace Insight.Application.Contracts;

public sealed record ActivityRecordedEnvelope(
    Guid EventId,
    string? EventType,
    int SchemaVersion,
    DateTime OccurredAtUtc,
    Guid CorrelationId,
    ActivityRecordedPayload? Payload);

public sealed record ActivityRecordedPayload(
    Guid EmployeeId,
    Guid SubmissionId,
    Guid? HabitId,
    Guid? ChallengeId,
    DateOnly ActivityDate,
    decimal Quantity);

public enum ActivityCategory
{
    Habit,
    Challenge
}

public sealed record ActivitySummaryDay(
    DateOnly ActivityDate,
    int HabitActivityCount,
    int ChallengeActivityCount);
