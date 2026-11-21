using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Challenge.UnitTests;

public sealed class ChallengeApiFactory(
    string connectionString,
    bool consumerEnabled = false,
    string? rabbitMqUri = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ChallengeDatabase", connectionString);
        builder.UseSetting("ChallengeConsumer:Enabled", consumerEnabled ? "true" : "false");
        builder.UseSetting("ChallengeConsumer:Uri", rabbitMqUri ?? string.Empty);
        builder.UseSetting("ChallengeConsumer:ReconnectDelayMilliseconds", "250");
        builder.UseSetting("ChallengeConsumer:ConnectionTimeoutMilliseconds", "1000");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ChallengeDatabase"] = connectionString,
                ["ChallengeConsumer:Enabled"] = consumerEnabled ? "true" : "false",
                ["ChallengeConsumer:Uri"] = rabbitMqUri ?? string.Empty,
                ["ChallengeConsumer:ReconnectDelayMilliseconds"] = "250",
                ["ChallengeConsumer:ConnectionTimeoutMilliseconds"] = "1000"
            });
        });
    }
}
