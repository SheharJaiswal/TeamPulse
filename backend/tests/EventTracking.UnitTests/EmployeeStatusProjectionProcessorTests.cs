using System;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
using Xunit;
namespace EventTracking.UnitTests;
public sealed class EmployeeStatusProjectionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MapsValidDeactivation_WithoutPersistingOptionalNote()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", employeeId, note: "private HR note"), CancellationToken.None));
        Assert.Equal(employeeId, store.LastEvent!.EmployeeId);
        Assert.False(store.LastEvent.IsActive);
        Assert.Equal(1, store.LastEvent.LifecycleVersion);
    }
    [Fact]
    public async Task ProcessAsync_MapsValidReactivation()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await processor.ProcessAsync(CreateMessage("EmployeeReactivated", "inactive", "active", Guid.NewGuid()), CancellationToken.None));
        Assert.True(store.LastEvent!.IsActive);
    }
    [Theory]
    [InlineData("EmployeeDeactivated", "inactive", "active")]
    [InlineData("EmployeeReactivated", "active", "inactive")]
    [InlineData("EmployeeCreated", "active", "active")]
    public async Task ProcessAsync_RejectsUnsupportedOrInconsistentTransitions(string eventType, string previousStatus, string newStatus)
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage(eventType, previousStatus, newStatus, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }
    [Fact]
    public async Task ProcessAsync_RejectsInvalidProducerAndReasonCode()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), source: "manual"), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), reasonCode: new string('x', 101)), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }
    [Fact]
    public async Task ProcessAsync_ClassifiesUnsupportedSchemaAndMalformedJson()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync("{", CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.UnsupportedSchemaVersion,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), schemaVersion: 2), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }
    [Fact]
    public async Task ProcessAsync_RejectsMissingEnvelopeFieldsAndNonPositiveVersion()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), lifecycleVersion: 0), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync("{\"eventType\":\"EmployeeDeactivated\",\"schemaVersion\":1}", CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }
    private static string CreateMessage(string eventType, string previousStatus, string newStatus, Guid employeeId,
        int schemaVersion = 1, long lifecycleVersion = 1, string source = "hr-admin-api",
        string reasonCode = "employment-change", string? note = null)
    {
        var noteJson = note is null ? "" : ",\"note\":\"" + note + "\"";
        return "{\"eventId\":\"" + Guid.NewGuid().ToString("D") + "\",\"eventType\":\"" + eventType +
            "\",\"schemaVersion\":" + schemaVersion + ",\"occurredAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") +
            "\",\"correlationId\":\"" + Guid.NewGuid().ToString("D") + "\",\"payload\":{\"employeeId\":\"" +
            employeeId.ToString("D") + "\",\"actorEmployeeId\":\"" + Guid.NewGuid().ToString("D") +
            "\",\"source\":\"" + source + "\",\"previousStatus\":\"" + previousStatus +
            "\",\"newStatus\":\"" + newStatus + "\",\"lifecycleVersion\":" + lifecycleVersion +
            ",\"reasonCode\":\"" + reasonCode + "\"" + noteJson + "}}";
    }
    private sealed class RecordingStore : IEmployeeStatusProjectionStore
    {
        public int CallCount { get; private set; }
        public EmployeeLifecycleEvent? LastEvent { get; private set; }
        public Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
        { CallCount++; LastEvent = lifecycleEvent; return Task.FromResult(ProjectionProcessingOutcome.Applied); }
    }
}