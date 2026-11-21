using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environmentName;

    public IdentityApiFactory(string connectionString, string environmentName = "Development")
    {
        _connectionString = connectionString;
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.UseSetting("ConnectionStrings:IdentityDatabase", _connectionString);
        builder.UseSetting("Jwt:Issuer", "TeamPulse.Identity.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "TeamPulse.Services.IntegrationTests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDatabase"] = _connectionString,
                ["Jwt:Issuer"] = "TeamPulse.Identity.IntegrationTests",
                ["Jwt:Audience"] = "TeamPulse.Services.IntegrationTests",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["InvitationEmail:Enabled"] = "false"
            });
        });
    }
}
