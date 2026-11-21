using System.Threading.Tasks;
using Testcontainers.MySql;
using Xunit;
namespace EventTracking.UnitTests;
public sealed class EventTrackingMySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _database = new MySqlBuilder().WithImage("mysql:8.4")
        .WithDatabase("teampulse_event_tracking_test").WithUsername("teampulse_test")
        .WithPassword("teampulse_test_password").Build();
    public string ConnectionString => _database.GetConnectionString();
    public Task InitializeAsync() => _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}