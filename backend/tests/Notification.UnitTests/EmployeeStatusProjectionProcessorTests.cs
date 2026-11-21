using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Notification.Application;
using Notification.Application.Abstractions;
using Notification.Application.Contracts;
using Xunit;
namespace Notification.UnitTests;
public sealed class EmployeeStatusProjectionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MapsValidDeactivation_AndDoesNotPersistOptionalNote()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        var message = CreateMessage("EmployeeDeactivated", "active", "inactive", employeeId, note: "private HR note");
        Assert.Equal(ProjectionProcessingOutcome.Applied, await processor.ProcessAsync(message, CancellationToken.None));
        Assert.Equal(employeeId, store.LastEvent!.EmployeeId);
        Assert.False(store.LastEvent.IsActive);
        Assert.Equal(1, store.LastEvent.LifecycleVersion);
        Assert.DoesNotContain("private HR note", JsonSerializer.Serialize(store.LastEvent));
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
    public async Task ProcessAsync_RejectsInvalidSourceAndReasonCode()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), source: "HR-ADMIN-API"), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), reasonCode: new string('x', 101)), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), reasonCode: "   "), CancellationToken.None));
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
    public async Task ProcessAsync_RejectsMissingFieldsAndNonPositiveVersion()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid(), lifecycleVersion: 0), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync("{\"eventType\":\"EmployeeDeactivated\",\"schemaVersion\":1}", CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ToleratesUnknownAdditiveProperties()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var message = CreateMessage("EmployeeDeactivated", "active", "inactive", Guid.NewGuid())
            .Replace("\"payload\":{", "\"payload\":{\"futurePayloadField\":{\"ignored\":true},")
            .Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"futureEnvelopeField\":\"ignored\"");
        Assert.Equal(ProjectionProcessingOutcome.Applied, await processor.ProcessAsync(message, CancellationToken.None));
        Assert.Equal(1, store.CallCount);
    }

    private static string CreateMessage(string eventType, string previousStatus, string newStatus, Guid employeeId,
        int schemaVersion = 1, long lifecycleVersion = 1, string source = "hr-admin-api",
        string reasonCode = "employment-change", string? note = null)
    {
        var envelope = new
        {
            eventId = Guid.NewGuid(),
            eventType,
            schemaVersion,
            occurredAtUtc = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid(),
            payload = new
            {
                employeeId,
                actorEmployeeId = Guid.NewGuid(),
                source,
                previousStatus,
                newStatus,
                lifecycleVersion,
                reasonCode,
                note
            }
        };
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
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