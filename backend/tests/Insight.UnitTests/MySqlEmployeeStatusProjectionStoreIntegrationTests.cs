using Insight.Application.Contracts;
using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using Xunit;

namespace Insight.UnitTests;

public sealed class MySqlEmployeeStatusProjectionStoreIntegrationTests
{
    [Fact]
    public async Task ProcessAsync_AtomicallyStoresProjectionAndInbox_AndDeduplicates()
    {
        await using var mysql = await CreateDatabaseAsync();
        var options = CreateOptions(mysql);
        await using (var setup = new InsightActivityDbContext(options)) await setup.Database.MigrateAsync();
        var store = new MySqlEmployeeStatusProjectionStore(new TestDbContextFactory(options));
        var employeeId = Guid.NewGuid();
        var item = CreateEvent(Guid.NewGuid(), employeeId, 1, false);
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(item, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, await store.ProcessAsync(item, DateTime.UtcNow, CancellationToken.None));
        await using var verify = new InsightActivityDbContext(options);
        var projection = await verify.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(1, await verify.EmployeeStatusProcessedEvents.CountAsync(x => x.EventId == item.EventId));
    }

    [Fact]
    public async Task ProcessAsync_StaleAndEqualEventsDoNotRegressProjection()
    {
        await using var mysql = await CreateDatabaseAsync();
        var options = CreateOptions(mysql);
        await using (var setup = new InsightActivityDbContext(options)) await setup.Database.MigrateAsync();
        var store = new MySqlEmployeeStatusProjectionStore(new TestDbContextFactory(options));
        var employeeId = Guid.NewGuid();
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, true), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, true), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 5, true), DateTime.UtcNow, CancellationToken.None));
        await using var verify = new InsightActivityDbContext(options);
        var projection = await verify.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(5, projection.LifecycleVersion);
        Assert.True(projection.IsActive);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentVersionsPreserveMaximumVersion()
    {
        await using var mysql = await CreateDatabaseAsync();
        var options = CreateOptions(mysql);
        await using (var setup = new InsightActivityDbContext(options)) await setup.Database.MigrateAsync();
        var store1 = new MySqlEmployeeStatusProjectionStore(new TestDbContextFactory(options));
        var store2 = new MySqlEmployeeStatusProjectionStore(new TestDbContextFactory(options));
        var employeeId = Guid.NewGuid();
        var outcomes = await Task.WhenAll(Enumerable.Range(1, 12).Select(version =>
            (version % 2 == 0 ? store1 : store2).ProcessAsync(
                CreateEvent(Guid.NewGuid(), employeeId, version, version % 2 == 0), DateTime.UtcNow, CancellationToken.None)));
        Assert.All(outcomes, outcome => Assert.Contains(outcome, new[] { ProjectionProcessingOutcome.Applied, ProjectionProcessingOutcome.Stale }));
        await using var verify = new InsightActivityDbContext(options);
        var projection = await verify.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(12, projection.LifecycleVersion);
        Assert.True(projection.IsActive);
    }

    private static async Task<MySqlContainer> CreateDatabaseAsync()
    {
        var mysql = new MySqlBuilder().WithImage("mysql:8.4").WithDatabase("teampulse_insight_test")
            .WithUsername("insight_test").WithPassword("insight_test_password").Build();
        await mysql.StartAsync();
        return mysql;
    }
    private static DbContextOptions<InsightActivityDbContext> CreateOptions(MySqlContainer mysql) =>
        new DbContextOptionsBuilder<InsightActivityDbContext>()
            .UseMySql(mysql.GetConnectionString(), new MySqlServerVersion(new Version(8, 4, 0))).Options;
    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool active) =>
        new(eventId, active ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId, active, version, Guid.NewGuid(), DateTime.UtcNow);
    private sealed class TestDbContextFactory(DbContextOptions<InsightActivityDbContext> options) : IDbContextFactory<InsightActivityDbContext>
    {
        public InsightActivityDbContext CreateDbContext() => new(options);
        public Task<InsightActivityDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new InsightActivityDbContext(options));
    }
}