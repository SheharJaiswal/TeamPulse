using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Contracts;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EventTracking.UnitTests;
public sealed class EmployeeStatusProjectionStoreIntegrationTests(EventTrackingMySqlFixture fixture) : IClassFixture<EventTrackingMySqlFixture>
{
    [Fact]
    public async Task ProcessAsync_PersistsProjectionAndInboxAtomically_AndDeduplicatesEventId()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), employeeId, 1, false);
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == lifecycleEvent.EventId));
    }
    [Fact]
    public async Task ProcessAsync_RecordsStaleVersionWithoutRegressingProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, true), DateTime.UtcNow, CancellationToken.None));
        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(4, projection.LifecycleVersion);
        Assert.Equal(2, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }
    [Fact]
    public async Task MigrateAsync_IsIdempotent()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        await database.Database.MigrateAsync();
        Assert.Equal(3, (await database.Database.GetAppliedMigrationsAsync()).Count());
        Assert.True(await database.EventTrackingOutboxMessages.AnyAsync() == false);
    }
    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<EventTrackingDbContext>(options => options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));
        var provider = services.BuildServiceProvider();
        await using var database = await provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>().CreateDbContextAsync();
        await database.Database.MigrateAsync();
        return provider;
    }
    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool isActive) =>
        new(eventId, isActive ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId, isActive, version, Guid.NewGuid(), DateTime.UtcNow);
}
