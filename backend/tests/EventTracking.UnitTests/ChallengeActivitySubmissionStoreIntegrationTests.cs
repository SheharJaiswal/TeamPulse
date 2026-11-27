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

public sealed class ChallengeActivitySubmissionStoreIntegrationTests(EventTrackingMySqlFixture fixture) : IClassFixture<EventTrackingMySqlFixture>
{
    [Fact]
    public async Task AddAsync_IsIdempotentAndAtomicallyCreatesChallengeOutboxEvent()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
        var store = new ChallengeActivitySubmissionStore(factory, TimeProvider.System);
        var employeeId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 27);
        var first = new ChallengeActivitySubmission(Guid.NewGuid(), employeeId, challengeId, date, 5000m, key, DateTime.UtcNow);
        var accepted = await store.AddAsync(first, CancellationToken.None);
        var retry = new ChallengeActivitySubmission(Guid.NewGuid(), employeeId, challengeId, date, 5000m, key, DateTime.UtcNow);
        var retried = await store.AddAsync(retry, CancellationToken.None);
        var second = new ChallengeActivitySubmission(Guid.NewGuid(), employeeId, challengeId, date, 2500.5m, Guid.NewGuid(), DateTime.UtcNow);
        await store.AddAsync(second, CancellationToken.None);

        Assert.Equal(first.Id, accepted.Id);
        Assert.Equal(first.Id, retried.Id);
        var history = await store.GetMineAsync(employeeId, challengeId, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal(7500.5m, history.Sum(x => x.Quantity));
        Assert.Empty(await store.GetMineAsync(Guid.NewGuid(), challengeId, CancellationToken.None));

        await using var database = await factory.CreateDbContextAsync();
        Assert.Equal(2, await database.ChallengeActivitySubmissions.CountAsync(x => x.EmployeeId == employeeId && x.ChallengeId == challengeId));
        var outbox = await database.EventTrackingOutboxMessages.SingleAsync(x => x.Payload.Contains(first.Id.ToString()));
        Assert.Equal("ChallengeActivityRecorded", outbox.EventType);
        using var envelope = JsonDocument.Parse(outbox.Payload);
        Assert.Equal("ChallengeActivityRecorded", envelope.RootElement.GetProperty("eventType").GetString());
        var payload = envelope.RootElement.GetProperty("payload");
        Assert.Equal(challengeId, payload.GetProperty("challengeId").GetGuid());
        Assert.Equal(employeeId, payload.GetProperty("employeeId").GetGuid());
        Assert.Equal(5000m, payload.GetProperty("quantity").GetDecimal());
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
