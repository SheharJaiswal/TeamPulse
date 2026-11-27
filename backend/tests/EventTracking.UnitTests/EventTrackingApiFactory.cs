using System.Collections.Generic;
using System.Net.Http;
using EventTracking.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventTracking.UnitTests;

public sealed class EventTrackingApiFactory(string connectionString, string? rabbitMqUri = null, Func<HttpMessageHandler>? habitApiHandlerFactory = null, Func<HttpMessageHandler>? challengeApiHandlerFactory = null) : WebApplicationFactory<Program>
{
    private const string JwtSigningKey = "event-tracking-integration-test-signing-key-32-bytes-minimum";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:EventTrackingDatabase", connectionString);
        builder.UseSetting("Jwt:Issuer", "TeamPulse.Identity");
        builder.UseSetting("Jwt:Audience", "TeamPulse.Services");
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventTrackingDatabase"] = connectionString,
                ["Jwt:Issuer"] = "TeamPulse.Identity",
                ["Jwt:Audience"] = "TeamPulse.Services",
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["HabitApi:BaseUrl"] = "http://localhost:5102/",
                ["ChallengeApi:BaseUrl"] = "http://localhost:5104/",
                ["EventTrackingOutbox:Enabled"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            if (habitApiHandlerFactory is not null)
                services.AddHttpClient("HabitApi").ConfigurePrimaryHttpMessageHandler(habitApiHandlerFactory);
            if (challengeApiHandlerFactory is not null)
                services.AddHttpClient("ChallengeApi").ConfigurePrimaryHttpMessageHandler(challengeApiHandlerFactory);
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
