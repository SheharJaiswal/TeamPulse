using System.Text.Json;
using Insight.Application;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;
using Xunit;

namespace Insight.UnitTests;

public sealed class EmployeeStatusProjectionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MapsDeactivation_AndExcludesNoteFromPersistenceContract()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        var outcome = await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", employeeId, note: "private HR note"), CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Applied, outcome);
        Assert.Equal(employeeId, store.LastEvent!.EmployeeId);
        Assert.False(store.LastEvent.IsActive);
        Assert.DoesNotContain("private HR note", JsonSerializer.Serialize(store.LastEvent));
    }

    [Fact]
    public async Task ProcessAsync_MapsReactivation()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var outcome = await processor.ProcessAsync(CreateMessage("EmployeeReactivated", "inactive", "active", Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Applied, outcome);
        Assert.True(store.LastEvent!.IsActive);
    }

    [Theory]
    [InlineData("EmployeeDeactivated", "inactive", "active")]
    [InlineData("EmployeeReactivated", "active", "inactive")]
    [InlineData("EmployeeCreated", "active", "active")]
    public async Task ProcessAsync_RejectsUnsupportedOrInconsistentTransitions(string type, string previous, string next)
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage(type, previous, next, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidNoteBeforePersistence()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), note: new string('n', 1001)), CancellationToken.None));
        var invalidType = CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid()).Replace("\"note\":null", "\"note\":42");
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(invalidType, CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidSourceReasonAndMalformedJson()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync("{", CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), source: "HR-ADMIN-API"), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), reasonCode: new string('x', 101)), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsUnsupportedSchemaAndToleratesAdditiveFields()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.UnsupportedSchemaVersion, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), schemaVersion: 2), CancellationToken.None));
        var additive = CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid())
            .Replace("\"payload\":{", "\"payload\":{\"newField\":true,")
            .Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"newEnvelopeField\":true");
        Assert.Equal(ProjectionProcessingOutcome.Applied, await processor.ProcessAsync(additive, CancellationToken.None));
        Assert.Equal(1, store.CallCount);
    }

    private static string CreateMessage(string type, string previous, string next, Guid employeeId,
        int schemaVersion = 1, string source = "hr-admin-api", string reasonCode = "employment-change", string? note = null)
    {
        var value = new
        {
            eventId = Guid.NewGuid(), eventType = type, schemaVersion, occurredAtUtc = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid(),
            payload = new { employeeId, actorEmployeeId = Guid.NewGuid(), source, previousStatus = previous, newStatus = next, lifecycleVersion = 1L, reasonCode, note }
        };
        return JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed class RecordingStore : IEmployeeStatusProjectionStore
    {
        public int CallCount { get; private set; }
        public EmployeeLifecycleEvent? LastEvent { get; private set; }
        public Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
        {
            CallCount++;
            LastEvent = lifecycleEvent;
            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }
    }
}