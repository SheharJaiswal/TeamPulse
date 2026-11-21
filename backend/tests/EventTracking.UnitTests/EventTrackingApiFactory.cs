using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EventTracking.UnitTests;

public sealed class EventTrackingApiFactory(string connectionString, string? rabbitMqUri = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:EventTrackingDatabase", connectionString);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventTrackingDatabase"] = connectionString,
                ["EventTrackingConsumer:Enabled"] = (rabbitMqUri is not null).ToString(),
                ["EventTrackingConsumer:Uri"] = rabbitMqUri ?? string.Empty,
                ["EventTrackingConsumer:ReconnectDelayMilliseconds"] = "250",
                ["EventTrackingConsumer:ConnectionTimeoutMilliseconds"] = "1000",
                // Keep broker-loss detection deterministic in integration tests. Production
                // retains the five-second default unless explicitly configured.
                ["EventTrackingConsumer:HeartbeatSeconds"] = "1"
            });
        });
    }
}
