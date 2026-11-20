using System;
using System.Linq;
using Challenge.API.HealthChecks;
using Challenge.Application;
using Challenge.Application.Abstractions;
using Challenge.Infrastructure.Messaging;
using Challenge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ChallengeDatabase")
    ?? throw new InvalidOperationException("ChallengeDatabase connection string is missing.");
var consumerOptions = builder.Configuration.GetSection(ChallengeRabbitMqConsumerOptions.SectionName)
    .Get<ChallengeRabbitMqConsumerOptions>() ?? new ChallengeRabbitMqConsumerOptions();
consumerOptions.Validate();

builder.Services.AddDbContextFactory<ChallengeDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton<ChallengeRabbitMqConsumerStatus>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore, EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddHostedService<ChallengeRabbitMqConsumer>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("challenge-database",
        sp => new ChallengeDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<ChallengeDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)))
    .Add(new HealthCheckRegistration("challenge-rabbitmq-consumer",
        sp => new ChallengeRabbitMqConsumerHealthCheck(
            sp.GetRequiredService<ChallengeRabbitMqConsumerStatus>(),
            sp.GetRequiredService<ChallengeRabbitMqConsumerOptions>()),
        HealthStatus.Unhealthy, new[] { "consumer" }, TimeSpan.FromSeconds(3)));

var app = builder.Build();

await using (var database = await app.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health/consumer", new HealthCheckOptions { Predicate = check => check.Tags.Contains("consumer") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program { }