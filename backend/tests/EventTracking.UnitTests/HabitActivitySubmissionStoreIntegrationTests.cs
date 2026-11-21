using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Domain.Entities;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class HabitActivitySubmissionStoreIntegrationTests(EventTrackingMySqlFixture fixture) : IClassFixture<EventTrackingMySqlFixture>
{
    [Fact]
    public async Task AddAsync_PreservesMultipleDailySubmissionsAndScopesIdempotencyPerEmployee()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var store = new HabitActivitySubmissionStore(factory, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 21);
        var first = new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId, date, 2, key, DateTime.UtcNow);

        var accepted = await store.AddAsync(first, CancellationToken.None);
        var duplicate = new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId, date, 2, key, DateTime.UtcNow);
        var retried = await store.AddAsync(duplicate, CancellationToken.None);
        var secondDailySubmission = new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId,
            date, 3, Guid.NewGuid(), DateTime.UtcNow);
        await store.AddAsync(secondDailySubmission, CancellationToken.None);
        var anotherEmployee = Guid.NewGuid();
        var sameKeyForAnotherEmployee = new HabitActivitySubmission(Guid.NewGuid(), anotherEmployee, habitId,
            date, 2, key, DateTime.UtcNow);
        await store.AddAsync(sameKeyForAnotherEmployee, CancellationToken.None);

        Assert.Equal(first.Id, accepted.Id);
        Assert.Equal(first.Id, retried.Id);
        await using var database = await factory.CreateDbContextAsync();
        Assert.Equal(2, await database.HabitActivitySubmissions.CountAsync(x => x.EmployeeId == employeeId));
        Assert.Equal(1, await database.HabitActivitySubmissions.CountAsync(x => x.EmployeeId == anotherEmployee));
        Assert.Equal(3, await database.EventTrackingOutboxMessages.CountAsync(x =>
            x.Payload.Contains(first.Id.ToString()) ||
            x.Payload.Contains(secondDailySubmission.Id.ToString()) ||
            x.Payload.Contains(sameKeyForAnotherEmployee.Id.ToString())));
        var outbox = await database.EventTrackingOutboxMessages.SingleAsync(x => x.Payload.Contains(first.Id.ToString()));
        using var envelope = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("HabitActivityRecorded", envelope.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(first.Id, envelope.RootElement.GetProperty("payload").GetProperty("submissionId").GetGuid());
        Assert.Equal(employeeId, envelope.RootElement.GetProperty("payload").GetProperty("employeeId").GetGuid());
        var history = await store.GetMineAsync(employeeId, habitId, date, date, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Empty(await store.GetMineAsync(Guid.NewGuid(), habitId, date, date, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_RollsBackOutboxWhenSubmissionInsertFails()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var store = new HabitActivitySubmissionStore(factory, TimeProvider.System);
        var sharedSubmissionId = Guid.NewGuid();
        var first = new HabitActivitySubmission(sharedSubmissionId, Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 20), 1, Guid.NewGuid(), DateTime.UtcNow);
        await store.AddAsync(first, CancellationToken.None);

        var conflicting = new HabitActivitySubmission(sharedSubmissionId, Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 21), 3, Guid.NewGuid(), DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AddAsync(conflicting, CancellationToken.None));

        await using var database = await factory.CreateDbContextAsync();
        Assert.Single(await database.EventTrackingOutboxMessages.Where(x => x.Payload.Contains(sharedSubmissionId.ToString())).ToListAsync());
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<EventTrackingDbContext>(options =>
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));
        var provider = services.BuildServiceProvider();
        await using var database = await provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>().CreateDbContextAsync();
        await database.Database.MigrateAsync();
        return provider;
    }
}
