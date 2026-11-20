using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

internal sealed class IdentityStartupValidationFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "TeamPulse.Identity.StartupTests";
    private const string Audience = "TeamPulse.Services.StartupTests";
    private readonly string? _signingKey;

    public IdentityStartupValidationFactory(string? signingKey)
    {
        _signingKey = signingKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");

        if (_signingKey is not null)
        {
            builder.UseSetting("Jwt:SigningKey", _signingKey);
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // Mirror the proven test-host configuration used by IdentityApiFactory.
            // appsettings.json intentionally contains no signing key.
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:AccessTokenMinutes"] = "15"
            };

            if (_signingKey is not null)
            {
                settings["Jwt:SigningKey"] = _signingKey;
            }

            configuration.AddInMemoryCollection(settings);
        });
    }
}
