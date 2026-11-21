using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class EventTrackingApiStartupIntegrationTests(EventTrackingMySqlFixture fixture)
    : IClassFixture<EventTrackingMySqlFixture>
{
    [Fact]
    public async Task ApiHost_AppliesMigrationOnStartup_AndProductionProjectionReaderReadsPersistedState()
    {
        using var factory = new EventTrackingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);

        using var scope = factory.Services.CreateScope();
        var databaseFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        await using var database = await databaseFactory.CreateDbContextAsync();
        var appliedMigrations = await database.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261121130000_InitialEventTrackingProjection", appliedMigrations);

        var store = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionStore>();
        var reader = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionReader>();
        var employeeId = Guid.NewGuid();
        var processedAtUtc = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var lifecycleEvent = new EmployeeLifecycleEvent(
            Guid.NewGuid(),
            "EmployeeDeactivated",
            1,
            employeeId,
            false,
            3,
            Guid.NewGuid(),
            processedAtUtc);

        var outcome = await store.ProcessAsync(lifecycleEvent, processedAtUtc, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, outcome);
        var projection = await reader.GetByEmployeeIdAsync(employeeId, CancellationToken.None);
        Assert.NotNull(projection);
        Assert.Equal(employeeId, projection.EmployeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.Equal(processedAtUtc, projection.UpdatedAtUtc);
    }

    [Fact]
    public async Task ProductionProcessor_ReplayedEventUsesMySqlInboxAndDoesNotMutateProjectionTwice()
    {
        using var factory = new EventTrackingApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionProcessor>();
        var databaseFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var replayOnlyEmployeeId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc = DateTimeOffset.UtcNow;

        var firstDelivery = JsonSerializer.Serialize(new EmployeeLifecycleEnvelope
        {
            EventId = eventId,
            EventType = "EmployeeDeactivated",
            SchemaVersion = 1,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            Payload = new EmployeeLifecyclePayload
            {
                EmployeeId = employeeId,
                ActorEmployeeId = Guid.NewGuid(),
                Source = "hr-admin-api",
                PreviousStatus = "active",
                NewStatus = "inactive",
                LifecycleVersion = 3,
                ReasonCode = "policy_change"
            }
        });
        var conflictingReplay = JsonSerializer.Serialize(new EmployeeLifecycleEnvelope
        {
            EventId = eventId,
            EventType = "EmployeeReactivated",
            SchemaVersion = 1,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            Payload = new EmployeeLifecyclePayload
            {
                EmployeeId = replayOnlyEmployeeId,
                ActorEmployeeId = Guid.NewGuid(),
                Source = "hr-admin-api",
                PreviousStatus = "inactive",
                NewStatus = "active",
                LifecycleVersion = 99,
                ReasonCode = "replay_attempt"
            }
        });

        var firstOutcome = await processor.ProcessAsync(firstDelivery, CancellationToken.None);
        var replayOutcome = await processor.ProcessAsync(conflictingReplay, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, firstOutcome);
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, replayOutcome);

        await using var database = await databaseFactory.CreateDbContextAsync();
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == eventId));
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.False(await database.EmployeeStatusProjections.AnyAsync(x => x.EmployeeId == replayOnlyEmployeeId));
    }
}
