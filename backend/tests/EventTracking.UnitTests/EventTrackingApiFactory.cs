using System.Collections.Generic;
using EventTracking.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                ["ConnectionStrings:EventTrackingDatabase"] = connectionString
            });
        });
        // Configure the test consumer explicitly after the API's registrations. This avoids
        // a false-healthy health endpoint when test-host configuration binding leaves the
        // optional consumer disabled.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<EventTrackingRabbitMqConsumerOptions>();
            services.AddSingleton(new EventTrackingRabbitMqConsumerOptions
            {
                Enabled = rabbitMqUri is not null,
                Uri = rabbitMqUri ?? string.Empty,
                ReconnectDelayMilliseconds = 250,
                ConnectionTimeoutMilliseconds = 1000,
                HeartbeatSeconds = 1
            });
        });
    }
}
