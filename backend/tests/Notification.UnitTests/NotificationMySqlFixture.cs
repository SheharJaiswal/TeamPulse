using System.Threading.Tasks;
using Testcontainers.MySql;
using MySqlConnector;
using Xunit;
namespace Notification.UnitTests;
public sealed class NotificationMySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _database = new MySqlBuilder().WithImage("mysql:8.4")
        .WithDatabase("teampulse_notification_test").WithUsername("teampulse_test")
        .WithPassword("teampulse_test_password").Build();
    public string ConnectionString => new MySqlConnectionStringBuilder(_database.GetConnectionString())
    {
        UseAffectedRows = true
    }.ConnectionString;
    public Task InitializeAsync() => _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}