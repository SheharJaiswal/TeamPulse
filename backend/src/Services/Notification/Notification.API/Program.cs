using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.API.HealthChecks;
using Notification.Application;
using Notification.Application.Abstractions;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Messaging;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var consumerOptions = builder.Configuration.GetSection(NotificationRabbitMqConsumerOptions.SectionName)
    .Get<NotificationRabbitMqConsumerOptions>() ?? new NotificationRabbitMqConsumerOptions();
consumerOptions.Validate();

var configuredConnectionString = builder.Configuration.GetConnectionString("NotificationDatabase")
    ?? throw new InvalidOperationException("NotificationDatabase connection string is missing.");
var connectionString = new MySqlConnectionStringBuilder(configuredConnectionString)
{
    // No-op stale/equal upserts must report zero changed rows.
    UseAffectedRows = true
}.ConnectionString;

builder.Services.AddDbContextFactory<NotificationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(consumerOptions);
builder.Services.AddSingleton<NotificationRabbitMqConsumerStatus>();
builder.Services.AddHostedService<NotificationRabbitMqConsumer>();
builder.Services.AddScoped<EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionReader>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("notification-database",
        sp => new NotificationDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<NotificationDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)))
    .Add(new HealthCheckRegistration("notification-rabbitmq-consumer",
        sp => new NotificationRabbitMqConsumerHealthCheck(
            sp.GetRequiredService<NotificationRabbitMqConsumerStatus>(),
            sp.GetRequiredService<NotificationRabbitMqConsumerOptions>()),
        HealthStatus.Unhealthy, new[] { "consumer" }, TimeSpan.FromSeconds(3)));

var app = builder.Build();
await using (var database = await app.Services.GetRequiredService<IDbContextFactory<NotificationDbContext>>().CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health/consumer", new HealthCheckOptions { Predicate = check => check.Tags.Contains("consumer") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program { }