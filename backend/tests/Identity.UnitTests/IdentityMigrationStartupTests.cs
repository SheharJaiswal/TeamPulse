using System.Net;
using System.Net.Http.Json;
using Identity.Application.Contracts;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class IdentityMigrationStartupTests : IClassFixture<MySqlIntegrationFixture>
{
    private const string InitialMigration = "20261120030000_InitialIdentitySchema";
    private const string Password = "Strong-Integration-Password-123!";
    private readonly MySqlIntegrationFixture _fixture;

    public IdentityMigrationStartupTests(MySqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Startup_AppliesMigration_AndRepeatedStartupPreservesData()
    {
        // This class owns a dedicated fixture/container, so the database is empty on first startup.
        var email = $"restart-{Guid.NewGuid():N}@teampulse.test";
        using (var firstFactory = _fixture.CreateFactory())
        using (var firstClient = firstFactory.CreateClient())
        {
            var registration = await RegisterAsync(firstClient, email);
            Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

            await using var scope = firstFactory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var applied = (await database.Database.GetAppliedMigrationsAsync()).ToArray();

            Assert.Contains(InitialMigration, applied);
            Assert.Equal(1, applied.Count(x => x == InitialMigration));
            Assert.True(await database.Employees.AnyAsync(x => x.NormalizedEmail == email.ToUpperInvariant()));
        }

        // A second host exercises Database.MigrateAsync against an already-migrated schema.
        using (var secondFactory = _fixture.CreateFactory())
        using (var secondClient = secondFactory.CreateClient())
        {
            var health = await secondClient.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            await using var scope = secondFactory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var applied = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
            var employee = await database.Employees.SingleAsync(
                x => x.NormalizedEmail == email.ToUpperInvariant());

            Assert.Equal(1, applied.Count(x => x == InitialMigration));
            Assert.Equal(email, employee.Email);
        }
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        return client.PostAsJsonAsync("/api/identity/register", new RegisterRequest(
            email,
            Password,
            "Migration Test Employee",
            "org-migration",
            "team-migration"));
    }
}
