using System;
using System.Linq;
using Habit.API.HealthChecks;
using Habit.Application;
using Habit.Application.Abstractions;
using Habit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("HabitDatabase")
    ?? throw new InvalidOperationException("HabitDatabase connection string is missing.");

builder.Services.AddDbContextFactory<HabitDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IEmployeeStatusProjectionStore, EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration(
        "habit-database",
        serviceProvider => new HabitDatabaseHealthCheck(
            serviceProvider.GetRequiredService<IDbContextFactory<HabitDbContext>>()),
        HealthStatus.Unhealthy,
        new[] { "ready" },
        TimeSpan.FromSeconds(3)));

var app = builder.Build();

// Apply checked-in service-owned migrations before the host begins accepting requests.
// No RabbitMQ connection is required by this persistence/application foundation.
await using (var database = await app.Services
    .GetRequiredService<IDbContextFactory<HabitDbContext>>()
    .CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program { }
