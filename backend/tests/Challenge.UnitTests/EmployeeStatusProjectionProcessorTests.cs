using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Challenge.Domain.Entities;
using Xunit;

namespace Challenge.UnitTests;

public sealed class EmployeeStatusProjectionProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MapsValidDeactivation_WithoutCopyingPrivateNote()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        var result = await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", 4, employeeId), CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Applied, result);
        var captured = Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent);
        Assert.Equal(employeeId, captured.EmployeeId);
        Assert.False(captured.IsActive);
        Assert.Equal(4, captured.LifecycleVersion);
        Assert.Equal("rfc8785-sha256-v1", captured.FingerprintAlgorithm);
        Assert.Matches("^[0-9a-f]{64}$", captured.ContentFingerprint);
        Assert.DoesNotContain("private HR note", JsonSerializer.Serialize(captured));
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidSourceAndMalformedActor()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid(), source: "HR-Admin-Api"), CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid(), actor: "not-a-guid"), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ToleratesUnknownPropertiesAndExcludesNoteFromFingerprint()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var first = CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid(), note: "one", extra: ",\"futureField\":{\"a\":1}");
        var second = first.Replace("\"note\":\"one\"", "\"note\":\"two\"").Replace("\"a\":1", "\"a\":2");
        Assert.Equal(ProjectionProcessingOutcome.Applied, await processor.ProcessAsync(first, CancellationToken.None));
        var firstFingerprint = Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).ContentFingerprint;
        Assert.Equal(ProjectionProcessingOutcome.Applied, await processor.ProcessAsync(second, CancellationToken.None));
        Assert.Equal(firstFingerprint, Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).ContentFingerprint);
    }

    [Fact]
    public async Task ProcessAsync_FingerprintChangesWhenRecognizedContentChanges()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var first = CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid());
        var second = first.Replace("\"reasonCode\":\"employment-change\"", "\"reasonCode\":\"admin-change\"");
        await processor.ProcessAsync(first, CancellationToken.None);
        var firstFingerprint = Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).ContentFingerprint;
        await processor.ProcessAsync(second, CancellationToken.None);
        Assert.NotEqual(firstFingerprint, Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).ContentFingerprint);
    }

    [Fact]
    public async Task ProcessAsync_PreservesFullInt64LifecycleVersionInFingerprint()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", long.MaxValue, Guid.NewGuid()), CancellationToken.None);
        Assert.NotNull(Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).ContentFingerprint);
    }

    [Theory]
    [InlineData("EmployeeDeactivated", "inactive", "active")]
    [InlineData("EmployeeReactivated", "active", "inactive")]
    [InlineData("EmployeeCreated", "active", "active")]
    public async Task ProcessAsync_RejectsUnsupportedOrInconsistentTransitions(string eventType, string previousStatus, string newStatus)
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var result = await processor.ProcessAsync(CreateMessage(eventType, previousStatus, newStatus, 2, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, result);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsMalformedJsonAndUnsupportedSchema()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync("{", CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(
            CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid(), schemaVersion: 2), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonPositiveLifecycleVersion()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid, await processor.ProcessAsync(
            CreateMessage("EmployeeDeactivated", "active", "inactive", 0, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    private static string CreateMessage(string eventType, string previousStatus, string newStatus, long version, Guid employeeId,
        int schemaVersion = 1, string source = "hr-admin-api", string? actor = null, string note = "private HR note", string extra = "")
    {
        var actorId = actor ?? Guid.NewGuid().ToString("D");
        return "{\"eventId\":\"" + Guid.NewGuid().ToString("D") + "\",\"eventType\":\"" + eventType +
            "\",\"schemaVersion\":" + schemaVersion + ",\"occurredAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") +
            "\",\"correlationId\":\"" + Guid.NewGuid().ToString("D") + "\",\"payload\":{\"employeeId\":\"" +
            employeeId.ToString("D") + "\",\"actorEmployeeId\":\"" + actorId + "\",\"source\":\"" + source +
            "\",\"previousStatus\":\"" + previousStatus + "\",\"newStatus\":\"" + newStatus +
            "\",\"lifecycleVersion\":" + version + ",\"reasonCode\":\"employment-change\",\"note\":\"" + note + "\"}" + extra + "}";
    }

    private sealed class RecordingStore : IEmployeeStatusProjectionStore
    {
        public int CallCount { get; private set; }
        public EmployeeLifecycleEvent? LastEvent { get; private set; }
        public Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
        {
            CallCount++; LastEvent = lifecycleEvent;
            return Task.FromResult(ProjectionProcessingOutcome.Applied);
        }
        public Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult<EmployeeStatusProjection?>(null);
    }
}
