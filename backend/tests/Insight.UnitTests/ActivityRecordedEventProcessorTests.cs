using Xunit;
using System.Text.Json;
using Insight.Application;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;

namespace Insight.UnitTests;

public sealed class ActivityRecordedEventProcessorTests
{
    [Fact]
    public async Task ValidHabitEventIsMappedToHabitCategory()
    {
        var store = new RecordingActivityStore();
        var processor = new ActivityRecordedEventProcessor(store, TimeProvider.System);
        var json = JsonSerializer.Serialize(new ActivityRecordedEnvelope(
            Guid.NewGuid(), "HabitActivityRecorded", 1, DateTime.UtcNow, Guid.NewGuid(),
            new ActivityRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), 10)), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await processor.ProcessAsync(json, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, result);
        Assert.Equal(ActivityCategory.Habit, store.Category);
    }

    [Fact]
    public async Task ChallengeEventRequiresChallengeIdentifierAndIsMappedCorrectly()
    {
        var store = new RecordingActivityStore();
        var processor = new ActivityRecordedEventProcessor(store, TimeProvider.System);
        var json = JsonSerializer.Serialize(new ActivityRecordedEnvelope(
            Guid.NewGuid(), "ChallengeActivityRecorded", 1, DateTime.UtcNow, Guid.NewGuid(),
            new ActivityRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow), 2.5m)), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await processor.ProcessAsync(json, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, result);
        Assert.Equal(ActivityCategory.Challenge, store.Category);
    }

    [Fact]
    public async Task UnsupportedSchemaDoesNotCallStore()
    {
        var store = new RecordingActivityStore();
        var processor = new ActivityRecordedEventProcessor(store, TimeProvider.System);
        var json = JsonSerializer.Serialize(new ActivityRecordedEnvelope(
            Guid.NewGuid(), "HabitActivityRecorded", 2, DateTime.UtcNow, Guid.NewGuid(),
            new ActivityRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), 1)), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await processor.ProcessAsync(json, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.UnsupportedSchemaVersion, result);
        Assert.Null(store.Category);
    }

    private sealed class RecordingActivityStore : IActivitySummaryStore
    {
        public ActivityCategory? Category { get; private set; }

        public Task<ProjectionProcessingOutcome> ProcessAsync(
            ActivityRecordedEnvelope activity, ActivityCategory category, DateTime processedAtUtc,
            CancellationToken cancellationToken)
        {
            Category = category;
            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }

        public Task<IReadOnlyList<ActivitySummaryDay>> GetDailySummaryAsync(
            Guid employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ActivitySummaryDay>>(Array.Empty<ActivitySummaryDay>());
    }
}
