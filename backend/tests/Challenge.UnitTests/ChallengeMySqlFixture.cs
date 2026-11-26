using System.Threading.Tasks;
using Testcontainers.MySql;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeMySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _database = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("teampulse_challenge_test")
        .WithUsername("teampulse_test")
        .WithPassword("teampulse_test_password")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public ChallengeApiFactory CreateFactory() => new(ConnectionString);

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Apply the complete schema once before test cases run in parallel against this fixture.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var readiness = await client.GetAsync("/health/ready");
        readiness.EnsureSuccessStatusCode();
    }
    public async Task DisposeAsync() => await _database.DisposeAsync();
}