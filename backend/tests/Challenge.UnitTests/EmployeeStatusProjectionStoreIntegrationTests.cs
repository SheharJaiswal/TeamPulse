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
    public async Task ProcessAsync_ConcurrentSameEventIdAndFingerprint_CommitsOneInboxRecord()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), Guid.NewGuid(), 1, false);

        var results = await Task.WhenAll(
            Task.Run(() => store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None)),
            Task.Run(() => store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None)));

        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.Applied));
        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.Duplicate));

        await using var database = await factory.CreateDbContextAsync();
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == lifecycleEvent.EventId));
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == lifecycleEvent.EmployeeId);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.False(projection.IsActive);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentConflictingEventId_PreservesOnlyWinningFingerprintAndProjection()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var eventId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var original = CreateEvent(eventId, employeeId, 1, false, new string('a', 64));
        var conflicting = CreateEvent(eventId, employeeId, 99, true, new string('b', 64));

        var results = await Task.WhenAll(
            Task.Run(() => store.ProcessAsync(original, DateTime.UtcNow, CancellationToken.None)),
            Task.Run(() => store.ProcessAsync(conflicting, DateTime.UtcNow, CancellationToken.None)));

        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.Applied));
        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.DuplicateEventIntegrityViolation));
        var winner = results[0] == ProjectionProcessingOutcome.Applied ? original : conflicting;

        await using var database = await factory.CreateDbContextAsync();
        var inbox = await database.ProcessedDomainEvents.SingleAsync(x => x.EventId == eventId);
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(winner.ContentFingerprint, inbox.ContentFingerprint);
        Assert.Equal(winner.LifecycleVersion, projection.LifecycleVersion);
        Assert.Equal(winner.IsActive, projection.IsActive);
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == eventId));
    }

    [Fact]
    public async Task MigrateAsync_CanBeAppliedRepeatedlyWithoutChangingMigrationHistory()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        await using var database = await factory.CreateDbContextAsync();

        await database.Database.MigrateAsync();
        var appliedMigrations = await database.Database.GetAppliedMigrationsAsync();

        Assert.Equal(new[]
        {
            "20261120190000_InitialChallengeProjection",
            "20261120200000_AddInboxContentFingerprint",
            "20261126010000_AddChallenges",
            "20261126020000_AddChallengeParticipations",
            "20261127010000_AddChallengeRegistrationAndTeams"
        }, appliedMigrations);
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
    public async Task ProcessAsync_ConcurrentEqualVersions_OnlyOneProjectionMutationIsApplied()
    {
        var readBarrier = new ConcurrentProjectionReadBarrierInterceptor();
        using var provider = await CreateProviderAsync(readBarrier);
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var firstProcessedAt = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var secondProcessedAt = firstProcessedAt.AddSeconds(1);
        var first = CreateEvent(Guid.NewGuid(), employeeId, 3, false);
        var second = CreateEvent(Guid.NewGuid(), employeeId, 3, true);

        var results = await Task.WhenAll(
            store.ProcessAsync(first, firstProcessedAt, CancellationToken.None),
            store.ProcessAsync(second, secondProcessedAt, CancellationToken.None));
        readBarrier.Disable();

        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.Applied));
        Assert.Equal(1, results.Count(result => result == ProjectionProcessingOutcome.Stale));
        var winningEvent = results[0] == ProjectionProcessingOutcome.Applied ? first : second;
        var winningTimestamp = results[0] == ProjectionProcessingOutcome.Applied ? firstProcessedAt : secondProcessedAt;

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(3, projection.LifecycleVersion);
        Assert.Equal(winningEvent.IsActive, projection.IsActive);
        Assert.Equal(winningTimestamp.Ticks, projection.UpdatedAtUtc.Ticks);
        Assert.Equal(2, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_AuthoritativeActiveSnapshotRepairsProjectionAtSameVersion()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();

        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, false),
                DateTime.UtcNow, CancellationToken.None));

        var snapshot = CreateEvent(Guid.NewGuid(), employeeId, 4, true) with
        {
            EventType = "EmployeeActiveSnapshot",
            PreviousStatus = "active",
            NewStatus = "active"
        };
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(snapshot, DateTime.UtcNow, CancellationToken.None));

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.True(projection.IsActive);
        Assert.Equal(4, projection.LifecycleVersion);
        Assert.Equal(2, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_DelayedActiveSnapshotCannotUndoNewerDeactivation()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();

        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 5, false),
                DateTime.UtcNow, CancellationToken.None));

        var delayedSnapshot = CreateEvent(Guid.NewGuid(), employeeId, 4, true) with
        {
            EventType = "EmployeeActiveSnapshot",
            PreviousStatus = "active",
            NewStatus = "active"
        };
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(delayedSnapshot, DateTime.UtcNow, CancellationToken.None));

        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(5, projection.LifecycleVersion);
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

    // If a pre-upsert projection read is reintroduced, hold both concurrent reads
    // until they have observed the same initial version. This makes the old outcome
    // classification race deterministic. The current affected-row implementation
    // performs no such pre-read, so the barrier is disabled before verification reads.
    private sealed class ConcurrentProjectionReadBarrierInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> _bothReadsCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;
        private int _enabled = 1;

        public void Disable() => Interlocked.Exchange(ref _enabled, 0);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _enabled) == 1 &&
                command.CommandText.Contains("employee_status_projections", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("lifecycle_version", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref _readCount) == 2)
                    _bothReadsCompleted.TrySetResult(true);

                await _bothReadsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
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
