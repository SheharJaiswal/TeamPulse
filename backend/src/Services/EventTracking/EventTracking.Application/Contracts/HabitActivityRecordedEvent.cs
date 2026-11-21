using System;
namespace EventTracking.Application.Contracts;
public sealed record HabitActivityRecordedEvent(
    Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid EmployeeId, Guid SubmissionId, Guid HabitId, DateOnly ActivityDate, int Quantity);
