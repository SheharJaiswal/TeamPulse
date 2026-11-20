using Testcontainers.MySql;

namespace Identity.UnitTests;

public sealed class MySqlIntegrationFixture : IAsyncLifetime
{
    private readonly MySqlContainer _database = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("teampulse_identity_test")
        .WithUsername("teampulse_test")
        .WithPassword("teampulse_test_password")
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public Task InitializeAsync() => _database.StartAsync();

    public Task StopAsync() => _database.StopAsync();

    public Task StartAsync() => _database.StartAsync();

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    public IdentityApiFactory CreateFactory(string environmentName = "Development") =>
        new(ConnectionString, environmentName);
}
