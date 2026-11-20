using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

internal sealed class IdentityStartupValidationFactory : WebApplicationFactory<Program>
{
    private readonly bool _includeSigningKey;
    private readonly string? _signingKey;

    public IdentityStartupValidationFactory(bool includeSigningKey, string? signingKey = null)
    {
        _includeSigningKey = includeSigningKey;
        _signingKey = signingKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Remove appsettings and environment providers so the local placeholder key
            // cannot accidentally satisfy a missing-secret test.
            configuration.Sources.Clear();

            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TeamPulse.Identity.StartupTests",
                ["Jwt:Audience"] = "TeamPulse.Services.StartupTests",
                ["Jwt:AccessTokenMinutes"] = "15"
            };

            if (_includeSigningKey)
            {
                settings["Jwt:SigningKey"] = _signingKey;
            }

            configuration.AddInMemoryCollection(settings);
        });
    }
}
