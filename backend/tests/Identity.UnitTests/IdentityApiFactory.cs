using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Identity.API.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environmentName;
    private readonly IReadOnlyDictionary<string, string?> _additionalSettings;

    public IdentityApiFactory(
        string connectionString,
        string environmentName = "Development",
        IReadOnlyDictionary<string, string?>? additionalSettings = null)
    {
        _connectionString = connectionString;
        _environmentName = environmentName;
        _additionalSettings = additionalSettings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.UseSetting("ConnectionStrings:IdentityDatabase", _connectionString);
        builder.UseSetting("Jwt:Issuer", "TeamPulse.Identity.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "TeamPulse.Services.IntegrationTests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("OutboxDispatcher:Enabled", "false");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = _connectionString,
                ["Jwt:Issuer"] = "TeamPulse.Identity.IntegrationTests",
                ["Jwt:Audience"] = "TeamPulse.Services.IntegrationTests",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["InvitationEmail:Enabled"] = "false",
                ["OutboxDispatcher:Enabled"] = "false"
            }.Concat(_additionalSettings).GroupBy(pair => pair.Key)
                .ToDictionary(group => group.Key, group => group.Last().Value));
        });

        // Integration tests exercise the outbox store directly. Prevent the real
        // hosted dispatcher from racing those assertions and consuming seeded rows.
        builder.ConfigureTestServices(services =>
        {
            var dispatcher = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(OutboxDispatcherWorker));
            if (dispatcher is not null)
                services.Remove(dispatcher);
        });
    }
}
