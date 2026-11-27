using Insight.Application.Contracts;
using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MySql;
using Xunit;

namespace Insight.UnitTests;

public sealed class MySqlActivitySummaryStoreIntegrationTests
{
    [Fact]
    public async Task DuplicateEventDoesNotIncrementAndConcurrentEventsAreCounted()
    {
        await using var mysql = new MySqlBuilder()
            .WithDatabase("teampulse_insight_test")
            .WithUsername("insight_test")
            .WithPassword("insight_test_password")
            .Build();
        await mysql.StartAsync();

        var options = new DbContextOptionsBuilder<InsightActivityDbContext>()
            .UseMySql(mysql.GetConnectionString(), new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        await using (var setup = new InsightActivityDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var factory = new TestDbContextFactory(options);
        var store = new MySqlActivitySummaryStore(factory);
        var employeeId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 27);
        var duplicateId = Guid.NewGuid();
        var duplicateEvent = CreateEvent(duplicateId, employeeId, date);

        var first = await store.ProcessAsync(duplicateEvent, ActivityCategory.Habit,
            DateTime.UtcNow, CancellationToken.None);
        var duplicate = await store.ProcessAsync(duplicateEvent, ActivityCategory.Habit,
            DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(ProjectionProcessingOutcome.Applied, first);
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, duplicate);

        var events = Enumerable.Range(0, 20)
            .Select(_ => CreateEvent(Guid.NewGuid(), employeeId, date))
            .ToArray();
        await Task.WhenAll(events.Select(e => store.ProcessAsync(e, ActivityCategory.Challenge,
            DateTime.UtcNow, CancellationToken.None)));

        var summary = await store.GetDailySummaryAsync(employeeId, date, date, CancellationToken.None);
        var row = Assert.Single(summary);
        Assert.Equal(1, row.HabitActivityCount);
        Assert.Equal(20, row.ChallengeActivityCount);
    }

    private static ActivityRecordedEnvelope CreateEvent(Guid eventId, Guid employeeId, DateOnly date) =>
        new(eventId, "HabitActivityRecorded", 1, DateTime.UtcNow, Guid.NewGuid(),
            new ActivityRecordedPayload(employeeId, Guid.NewGuid(), Guid.NewGuid(), null, date, 1));

    private sealed class TestDbContextFactory(DbContextOptions<InsightActivityDbContext> options)
        : IDbContextFactory<InsightActivityDbContext>
    {
        public InsightActivityDbContext CreateDbContext() => new(options);

        public Task<InsightActivityDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InsightActivityDbContext(options));
    }
}
