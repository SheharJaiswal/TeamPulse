using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Contracts;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Habit.UnitTests;

public sealed class EmployeeStatusProjectionStoreIntegrationTests(HabitMySqlFixture fixture)
    : IClassFixture<HabitMySqlFixture>
{
    [Fact]
    public async Task ProcessAsync_DeduplicatesEventId_AndCommitsProjectionAndInbox()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), employeeId, 1, false);

        var first = await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None);
        var duplicate = await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, first);
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, duplicate);

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(lifecycleEvent.EmployeeId, projection.EmployeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));

        var readProjection = await store.GetByEmployeeIdAsync(employeeId, CancellationToken.None);
        Assert.NotNull(readProjection);
        Assert.Equal(1, readProjection!.LifecycleVersion);
    }

    [Fact]
    public async Task ProcessAsync_RecordsStaleEventWithoutRegressingProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();

        var newest = CreateEvent(Guid.NewGuid(), employeeId, 3, false);
        var stale = CreateEvent(Guid.NewGuid(), employeeId, 2, true);

        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(newest, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(stale, DateTime.UtcNow, CancellationToken.None));

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.False(projection.IsActive);
        Assert.Equal(2, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentVersionsNeverRegressProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var lower = CreateEvent(Guid.NewGuid(), employeeId, 8, true);
        var higher = CreateEvent(Guid.NewGuid(), employeeId, 9, false);

        var results = await Task.WhenAll(
            store.ProcessAsync(lower, DateTime.UtcNow, CancellationToken.None),
            store.ProcessAsync(higher, DateTime.UtcNow, CancellationToken.None));

        Assert.All(results, result => Assert.Contains(result,
            new[] { ProjectionProcessingOutcome.Applied, ProjectionProcessingOutcome.Stale }));

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(9, projection.LifecycleVersion);
        Assert.False(projection.IsActive);
        Assert.Equal(2, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }


    [Fact]
    public async Task ProcessAsync_WhenProjectionWriteFails_RollsBackInboxMarker()
    {
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(eventId, employeeId, 1, false);

        using (var failingProvider = await CreateProviderAsync(new FailProjectionInsertInterceptor()))
        {
            var factory = failingProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
            var store = new EmployeeStatusProjectionStore(factory);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        }

        using (var verificationProvider = await CreateProviderAsync())
        {
            var factory = verificationProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
            await using var database = await factory.CreateDbContextAsync();

            Assert.False(await database.ProcessedDomainEvents.AnyAsync(x => x.EventId == eventId));
            Assert.False(await database.EmployeeStatusProjections.AnyAsync(x => x.EmployeeId == employeeId));
        }
    }

    private async Task<ServiceProvider> CreateProviderAsync(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<HabitDbContext>(options =>
        {
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString));
            if (interceptors.Length > 0)
                options.AddInterceptors(interceptors);
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<HabitDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        await database.Database.MigrateAsync();
        return provider;
    }

    private sealed class FailProjectionInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(
                "INSERT INTO employee_status_projections", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Injected projection write failure.");

            return ValueTask.FromResult(result);
        }
    }

    private static EmployeeLifecycleEvent CreateEvent(
        Guid eventId, Guid employeeId, long version, bool isActive) =>
        new(eventId,
            isActive ? "EmployeeReactivated" : "EmployeeDeactivated",
            1,
            employeeId,
            isActive,
            version,
            Guid.NewGuid(),
            DateTime.UtcNow);
}
