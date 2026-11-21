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
    public Task InitializeAsync() => _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}