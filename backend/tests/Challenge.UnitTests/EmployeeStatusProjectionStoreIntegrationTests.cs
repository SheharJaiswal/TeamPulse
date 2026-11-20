using System;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Contracts;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Challenge.UnitTests;

public sealed class EmployeeStatusProjectionStoreIntegrationTests(ChallengeMySqlFixture fixture)
    : IClassFixture<ChallengeMySqlFixture>
{
    [Fact]
    public async Task ProcessAsync_DeduplicatesEventId_AndCommitsInboxAndProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var message = CreateEvent(Guid.NewGuid(), employeeId, 1, false);

        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(message, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, await store.ProcessAsync(message, DateTime.UtcNow, CancellationToken.None));

        await using var db = await factory.CreateDbContextAsync();
        var projection = await db.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(1, await db.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_RecordsStaleEventWithoutRegressingProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();

        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 2, true), DateTime.UtcNow, CancellationToken.None));

        await using var db = await factory.CreateDbContextAsync();
        var projection = await db.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.False(projection.IsActive);
        Assert.Equal(2, await db.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentVersionsNeverRegressProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();

        var results = await Task.WhenAll(
            store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 8, true), DateTime.UtcNow, CancellationToken.None),
            store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 9, false), DateTime.UtcNow, CancellationToken.None));
        Assert.All(results, result => Assert.Contains(result, new[] { ProjectionProcessingOutcome.Applied, ProjectionProcessingOutcome.Stale }));

        await using var db = await factory.CreateDbContextAsync();
        var projection = await db.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(9, projection.LifecycleVersion);
        Assert.False(projection.IsActive);
        Assert.Equal(2, await db.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ChallengeDbContext>(options =>
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        return provider;
    }

    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool isActive) =>
        new(eventId, isActive ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId,
            isActive, version, Guid.NewGuid(), DateTime.UtcNow);
}