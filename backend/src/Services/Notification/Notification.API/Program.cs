using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.API.HealthChecks;
using Notification.Application;
using Notification.Application.Abstractions;
using Notification.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("NotificationDatabase")
    ?? throw new InvalidOperationException("NotificationDatabase connection string is missing.");

builder.Services.AddDbContextFactory<NotificationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionReader>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("notification-database",
        sp => new NotificationDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<NotificationDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)));

var app = builder.Build();
await using (var database = await app.Services.GetRequiredService<IDbContextFactory<NotificationDbContext>>().CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program { }