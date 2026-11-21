using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Contracts;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Challenge.UnitTests;

public sealed class EmployeeStatusProjectionStoreIntegrationTests(ChallengeMySqlFixture fixture)
    : IClassFixture<ChallengeMySqlFixture>
{
    [Fact]
    public async Task InboxSchema_ContainsOnlyApprovedMetadataAndNoAutomaticCleanupEvents()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText =
            "SELECT COLUMN_NAME FROM information_schema.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'processed_domain_events' " +
            "ORDER BY ORDINAL_POSITION";
        var columns = new List<string>();
        await using (var reader = await columnsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));
        }

        Assert.Equal(new[]
        {
            "event_id", "event_type", "schema_version", "employee_id",
            "lifecycle_version", "processed_at_utc", "fingerprint_algorithm", "content_fingerprint"
        }, columns);

        await using var eventsCommand = connection.CreateCommand();
        eventsCommand.CommandText =
            "SELECT EVENT_NAME FROM information_schema.EVENTS WHERE EVENT_SCHEMA = DATABASE()";
        var scheduledEvents = new List<string>();
        await using (var reader = await eventsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                scheduledEvents.Add(reader.GetString(0));
        }

        Assert.Empty(scheduledEvents);
    }

    [Fact]
    public async Task ProcessAsync_DeduplicatesSameEventIdAndFingerprint()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), 1, false);
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate, await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_QuarantinesSameEventIdWithDifferentFingerprintWithoutMutation()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var first = CreateEvent(eventId, employeeId, 1, false, "a".PadLeft(64, '0'));
        var conflicting = CreateEvent(eventId, employeeId, 99, true, "b".PadLeft(64, '0'));
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(first, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.DuplicateEventIntegrityViolation,
            await store.ProcessAsync(conflicting, DateTime.UtcNow, CancellationToken.None));

        await using var db = await factory.CreateDbContextAsync();
        var projection = await db.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        var inbox = await db.ProcessedDomainEvents.SingleAsync(x => x.EventId == eventId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(first.ContentFingerprint, inbox.ContentFingerprint);
    }

    [Fact]
    public async Task ProcessAsync_RecordsStaleEventWithoutRegressingProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 2, true), DateTime.UtcNow, CancellationToken.None));
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

    [Fact]
    public async Task ProcessAsync_RecordsEqualVersionAsStaleWithoutChangingProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var firstProcessedAt = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, false), firstProcessedAt, CancellationToken.None));
        DateTime originalUpdatedAt;
        await using (var before = await factory.CreateDbContextAsync())
            originalUpdatedAt = (await before.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId)).UpdatedAtUtc;
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, true), firstProcessedAt.AddSeconds(1), CancellationToken.None));
        await using var after = await factory.CreateDbContextAsync();
        var projection = await after.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.Equal(originalUpdatedAt.Ticks, projection.UpdatedAtUtc.Ticks);
        Assert.Equal(2, await after.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_WhenProjectionWriteFails_RollsBackInboxMarker()
    {
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(eventId, employeeId, 1, false);
        using (var failingProvider = await CreateProviderAsync(new FailProjectionInsertInterceptor()))
        {
            var factory = failingProvider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
            var store = new EmployeeStatusProjectionStore(factory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        }
        using (var verificationProvider = await CreateProviderAsync())
        {
            var factory = verificationProvider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
            await using var database = await factory.CreateDbContextAsync();
            Assert.False(await database.ProcessedDomainEvents.AnyAsync(x => x.EventId == eventId));
            Assert.False(await database.EmployeeStatusProjections.AnyAsync(x => x.EmployeeId == employeeId));
        }
    }

    private sealed class FailProjectionInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO employee_status_projections", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Injected projection write failure.");
            return ValueTask.FromResult(result);
        }
    }

    private async Task<ServiceProvider> CreateProviderAsync(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<ChallengeDbContext>(options =>
        {
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString));
            if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        });
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        return provider;
    }

    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool isActive, string? fingerprint = null) =>
        new(eventId, isActive ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId,
            isActive, version, Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "hr-admin-api",
            isActive ? "inactive" : "active", isActive ? "active" : "inactive", "employment-change",
            "rfc8785-sha256-v1", fingerprint ?? Guid.NewGuid().ToString("N").PadLeft(64, '0'));
}
