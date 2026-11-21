using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Contracts;
using Notification.Infrastructure.Persistence;
using Xunit;
namespace Notification.UnitTests;
public sealed class EmployeeStatusProjectionStoreIntegrationTests(NotificationMySqlFixture fixture)
    : IClassFixture<NotificationMySqlFixture>
{
    [Fact]
    public async Task ProcessAsync_CommitsInboxAndProjection_AndDeduplicatesEventId()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<NotificationDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), employeeId, 1, false);
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate,
            await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(1, projection.LifecycleVersion);
        Assert.Equal(1, await database.ProcessedDomainEvents.CountAsync(x => x.EventId == lifecycleEvent.EventId));
    }

    [Fact]
    public async Task ProcessAsync_RecordsStaleAndEqualVersionsWithoutRegression()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<NotificationDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, true), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale,
            await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, true), DateTime.UtcNow, CancellationToken.None));
        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(4, projection.LifecycleVersion);
        Assert.Equal(3, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentVersions_PreservesMaximumLifecycleVersion()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<NotificationDbContext>>();
        var store = new EmployeeStatusProjectionStore(factory);
        var employeeId = Guid.NewGuid();
        var deliveries = Enumerable.Range(1, 12)
            .Select(version => store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, version, version % 2 == 0),
                DateTime.UtcNow, CancellationToken.None));
        await Task.WhenAll(deliveries);
        await using var database = await factory.CreateDbContextAsync();
        var projection = await database.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.Equal(12, projection.LifecycleVersion);
        Assert.True(projection.IsActive);
        Assert.Equal(12, await database.ProcessedDomainEvents.CountAsync(x => x.EmployeeId == employeeId));
    }

    [Fact]
    public async Task MigrateAsync_IsIdempotent_AndSchemaContainsOnlyApprovedFields()
    {
        using var provider = await CreateProviderAsync();
        var factory = provider.GetRequiredService<IDbContextFactory<NotificationDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        await database.Database.MigrateAsync();
        Assert.Single(await database.Database.GetAppliedMigrationsAsync());
        var projectionColumns = await GetColumnsAsync(database, "employee_status_projections");
        Assert.Equal(new[] { "employee_id", "is_active", "lifecycle_version", "updated_at_utc" }, projectionColumns);
        var inboxColumns = await GetColumnsAsync(database, "processed_domain_events");
        Assert.Equal(new[] { "event_id", "event_type", "schema_version", "employee_id", "lifecycle_version", "processed_at_utc" }, inboxColumns);
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<NotificationDbContext>(options =>
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));
        var provider = services.BuildServiceProvider();
        await using var database = await provider.GetRequiredService<IDbContextFactory<NotificationDbContext>>().CreateDbContextAsync();
        await database.Database.MigrateAsync();
        return provider;
    }

    private static async Task<string[]> GetColumnsAsync(NotificationDbContext database, string table)
    {
        var connection = database.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table ORDER BY ORDINAL_POSITION";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@table";
        parameter.Value = table;
        command.Parameters.Add(parameter);
        var columns = new System.Collections.Generic.List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        return columns.ToArray();
    }

    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool isActive) =>
        new(eventId, isActive ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId,
            isActive, version, Guid.NewGuid(), DateTime.UtcNow);
}