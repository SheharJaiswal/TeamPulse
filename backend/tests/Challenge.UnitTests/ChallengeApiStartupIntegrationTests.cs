using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeApiStartupIntegrationTests(ChallengeMySqlFixture fixture)
    : IClassFixture<ChallengeMySqlFixture>
{
    [Fact]
    public async Task ApiHost_AppliesMigrationsOnStartupAndProjectionReaderReadsMigratedDatabase()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ChallengeDbContext>();
        var appliedMigrations = await database.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20261120190000_InitialChallengeProjection", appliedMigrations);
        Assert.Contains("20261120200000_AddInboxContentFingerprint", appliedMigrations);

        var store = scope.ServiceProvider.GetRequiredService<IEmployeeStatusProjectionStore>();
        var employeeId = Guid.NewGuid();
        // MySQL datetime(6) persists microseconds; align the expected value to that precision.
        var processedAtUtc = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var lifecycleEvent = new EmployeeLifecycleEvent(
            Guid.NewGuid(),
            "EmployeeReactivated",
            1,
            employeeId,
            true,
            7,
            Guid.NewGuid(),
            processedAtUtc,
            Guid.NewGuid(),
            "hr-admin-api",
            "inactive",
            "active",
            "employee-reactivated",
            "sha-256-jcs-rfc8785-v1",
            new string('a', 64));

        var outcome = await store.ProcessAsync(lifecycleEvent, processedAtUtc, CancellationToken.None);
        Assert.Equal(ProjectionProcessingOutcome.Applied, outcome);

        var projection = await store.GetByEmployeeIdAsync(employeeId, CancellationToken.None);
        Assert.NotNull(projection);
        Assert.Equal(employeeId, projection.EmployeeId);
        Assert.True(projection.IsActive);
        Assert.Equal(7, projection.LifecycleVersion);
        Assert.Equal(processedAtUtc, projection.UpdatedAtUtc);
    }
}
