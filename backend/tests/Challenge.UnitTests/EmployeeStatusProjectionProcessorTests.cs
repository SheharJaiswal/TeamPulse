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
        Assert.DoesNotContain("private HR note", JsonSerializer.Serialize(captured));
    }

    [Fact]
    public async Task ProcessAsync_MapsValidReactivation()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        var result = await processor.ProcessAsync(CreateMessage("EmployeeReactivated", "inactive", "active", 5, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Applied, result);
        Assert.True(Assert.IsType<EmployeeLifecycleEvent>(store.LastEvent).IsActive);
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
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", 2, Guid.NewGuid(), 2), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RejectsNonPositiveLifecycleVersion()
    {
        var store = new RecordingStore();
        var processor = new EmployeeStatusProjectionProcessor(store, TimeProvider.System);
        Assert.Equal(ProjectionProcessingOutcome.Invalid,
            await processor.ProcessAsync(CreateMessage("EmployeeDeactivated", "active", "inactive", 0, Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, store.CallCount);
    }

    private static string CreateMessage(string eventType, string previousStatus, string newStatus, long version, Guid employeeId, int schemaVersion = 1) =>
        JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), eventType, schemaVersion, occurredAtUtc = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid(),
            payload = new { employeeId, actorEmployeeId = Guid.NewGuid(), source = "hr-admin-api", previousStatus,
                newStatus, lifecycleVersion = version, reasonCode = "employment-change", note = "private HR note" }
        });

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