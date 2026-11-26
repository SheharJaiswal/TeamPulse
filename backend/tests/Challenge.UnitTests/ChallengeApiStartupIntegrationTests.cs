using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Challenge.Domain.Entities;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;
using Challenge.Infrastructure.Persistence;
using Challenge.Infrastructure.Messaging;
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
        Assert.Contains("20261126010000_AddChallenges", appliedMigrations);
        Assert.Contains("20261126020000_AddChallengeParticipations", appliedMigrations);

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

    [Fact]
    public async Task ApiHost_ReportsHealthRoutesIndependentlyWhenRabbitMqIsUnavailable()
    {
        using var portReservation = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        portReservation.Start();
        var brokerPort = ((System.Net.IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();

        var brokerUri = $"amqp://guest:guest@127.0.0.1:{brokerPort}/";
        using var factory = new ChallengeApiFactory(
            fixture.ConnectionString,
            consumerEnabled: true,
            rabbitMqUri: brokerUri);
        using var client = factory.CreateClient();

        var consumerOptions = factory.Services.GetRequiredService<ChallengeRabbitMqConsumerOptions>();
        var consumerStatus = factory.Services.GetRequiredService<ChallengeRabbitMqConsumerStatus>();
        Assert.True(consumerOptions.Enabled);
        Assert.False(consumerStatus.IsConnected);

        using var liveness = await client.GetAsync("/health/live");
        using var readiness = await client.GetAsync("/health/ready");
        using var consumer = await client.GetAsync("/health/consumer");

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, consumer.StatusCode);
    }
    [Theory]
    [InlineData("Employee")]
    [InlineData("HrAdmin")]
    public async Task AvailableChallenges_DeniesAuthenticatedUserWhenLifecycleProjectionIsMissing(string role)
    {
        using var factory = fixture.CreateFactory();
        var employeeId = Guid.NewGuid();
        const string organizationId = "missing-projection-org";
        await using (var database = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            database.Challenges.Add(new Challenge.Domain.Entities.Challenge(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), "Active challenge", null,
                DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                DateTime.UtcNow));
            await database.SaveChangesAsync();
            Assert.False(await database.EmployeeStatusProjections.AnyAsync(x => x.EmployeeId == employeeId));
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(employeeId, organizationId, role));

        using var response = await client.GetAsync("/api/challenges/available");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var verify = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync();
        Assert.False(await verify.EmployeeStatusProjections.AnyAsync(x => x.EmployeeId == employeeId));
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("HrAdmin")]
    public async Task AvailableChallenges_AllowsEmployeeAndHrAdminWithActiveProjection(string role)
    {
        using var factory = fixture.CreateFactory();
        var employeeId = Guid.NewGuid();
        const string organizationId = "active-projection-org";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using (var database = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            database.EmployeeStatusProjections.Add(new EmployeeStatusProjection(
                employeeId, true, 1, DateTime.UtcNow));
            database.Challenges.Add(new Challenge.Domain.Entities.Challenge(
                Guid.NewGuid(), organizationId, Guid.NewGuid(), "Available challenge", null,
                today, today.AddDays(7), DateTime.UtcNow));
            await database.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(employeeId, organizationId, role));

        using var response = await client.GetAsync("/api/challenges/available");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Available challenge", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AvailableChallenges_DoesNotOverwriteExistingInactiveProjection()
    {
        using var factory = fixture.CreateFactory();
        var employeeId = Guid.NewGuid();
        const string organizationId = "projection-inactive-org";
        await using (var database = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            database.EmployeeStatusProjections.Add(new EmployeeStatusProjection(
                employeeId, false, 3, DateTime.UtcNow));
            await database.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(employeeId, organizationId, "Employee"));

        using var response = await client.GetAsync("/api/challenges/available");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);

        await using var verify = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync();
        var projection = await verify.EmployeeStatusProjections.SingleAsync(x => x.EmployeeId == employeeId);
        Assert.False(projection.IsActive);
        Assert.Equal(3, projection.LifecycleVersion);
    }

}
