using System.Linq;
using System.Threading.Tasks;
using Challenge.API.HealthChecks;
using Challenge.Infrastructure.Messaging;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeHealthCheckIntegrationTests(ChallengeMySqlFixture fixture)
    : IClassFixture<ChallengeMySqlFixture>
{
    [Fact]
    public async Task ReadinessRemainsHealthyWhenRabbitMqIsDisconnectedAndConsumerHealthIsSeparate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<ChallengeDbContext>(options =>
            options.UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString)));

        var databaseFactoryProvider = services.BuildServiceProvider();
        var databaseFactory = databaseFactoryProvider.GetRequiredService<IDbContextFactory<ChallengeDbContext>>();
        await using (var database = await databaseFactory.CreateDbContextAsync())
            await database.Database.MigrateAsync();

        var consumerStatus = new ChallengeRabbitMqConsumerStatus();
        var consumerOptions = new ChallengeRabbitMqConsumerOptions { Enabled = true };

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .Add(new HealthCheckRegistration(
                "challenge-database",
                _ => new ChallengeDatabaseHealthCheck(databaseFactory),
                HealthStatus.Unhealthy,
                new[] { "ready" }))
            .Add(new HealthCheckRegistration(
                "challenge-rabbitmq-consumer",
                _ => new ChallengeRabbitMqConsumerHealthCheck(consumerStatus, consumerOptions),
                HealthStatus.Unhealthy,
                new[] { "consumer" }));

        using var provider = services.BuildServiceProvider();
        var healthChecks = provider.GetRequiredService<HealthCheckService>();

        var liveness = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("live"));
        var readiness = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"));
        var consumer = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("consumer"));

        Assert.Equal(HealthStatus.Healthy, liveness.Status);
        Assert.Equal(HealthStatus.Healthy, readiness.Status);
        Assert.Contains("challenge-database", readiness.Entries.Keys);
        Assert.DoesNotContain("challenge-rabbitmq-consumer", readiness.Entries.Keys);
        Assert.Equal(HealthStatus.Unhealthy, consumer.Status);
        Assert.Contains("challenge-rabbitmq-consumer", consumer.Entries.Keys);
    }
}
