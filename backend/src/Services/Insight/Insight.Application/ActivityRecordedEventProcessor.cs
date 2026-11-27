using System.Text.Json;
using System.Text.Json.Serialization;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;

namespace Insight.Application;

public sealed class ActivityRecordedEventProcessor(
    IActivitySummaryStore store,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ProjectionProcessingOutcome.Invalid;

        ActivityRecordedEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ActivityRecordedEnvelope>(message, JsonOptions);
        }
        catch (JsonException)
        {
            return ProjectionProcessingOutcome.Invalid;
        }

        if (envelope is null || envelope.Payload is null)
            return ProjectionProcessingOutcome.Invalid;
        if (envelope.SchemaVersion != 1)
            return ProjectionProcessingOutcome.UnsupportedSchemaVersion;

        var payload = envelope.Payload;
        if (envelope.EventId == Guid.Empty ||
            envelope.CorrelationId == Guid.Empty ||
            envelope.OccurredAtUtc == default ||
            envelope.OccurredAtUtc.Kind != DateTimeKind.Utc ||
            payload.EmployeeId == Guid.Empty ||
            payload.SubmissionId == Guid.Empty ||
            payload.ActivityDate == default ||
            payload.Quantity <= 0)
            return ProjectionProcessingOutcome.Invalid;

        ActivityCategory category;
        if (string.Equals(envelope.EventType, "HabitActivityRecorded", StringComparison.Ordinal) &&
            payload.HabitId is { } habitId && habitId != Guid.Empty &&
            payload.ChallengeId is null)
            category = ActivityCategory.Habit;
        else if (string.Equals(envelope.EventType, "ChallengeActivityRecorded", StringComparison.Ordinal) &&
            payload.ChallengeId is { } challengeId && challengeId != Guid.Empty &&
            payload.HabitId is null)
            category = ActivityCategory.Challenge;
        else
            return ProjectionProcessingOutcome.Invalid;

        return await store.ProcessAsync(
            envelope,
            category,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }
}
