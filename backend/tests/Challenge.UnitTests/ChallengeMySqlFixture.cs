using System.Threading.Tasks;
using MySqlConnector;
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

    public string ConnectionString => new MySqlConnectionStringBuilder(_database.GetConnectionString())
    {
        UseAffectedRows = true
    }.ConnectionString;

    public ChallengeApiFactory CreateFactory() => new(ConnectionString);
    public Task InitializeAsync() => _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}