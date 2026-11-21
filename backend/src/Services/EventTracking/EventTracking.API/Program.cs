using System;
using System.Linq;
using EventTracking.API.HealthChecks;
using EventTracking.Application;
using EventTracking.Application.Abstractions;
using EventTracking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("EventTrackingDatabase")
    ?? throw new InvalidOperationException("EventTrackingDatabase connection string is missing.");
builder.Services.AddDbContextFactory<EventTrackingDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EmployeeStatusProjectionStore>();
builder.Services.AddScoped<IEmployeeStatusProjectionStore>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionReader>(sp => sp.GetRequiredService<EmployeeStatusProjectionStore>());
builder.Services.AddScoped<IEmployeeStatusProjectionProcessor, EmployeeStatusProjectionProcessor>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .Add(new HealthCheckRegistration("event-tracking-database",
        sp => new EventTrackingDatabaseHealthCheck(sp.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>()),
        HealthStatus.Unhealthy, new[] { "ready" }, TimeSpan.FromSeconds(3)));
var app = builder.Build();
await using (var database = await app.Services.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>().CreateDbContextAsync())
{
    await database.Database.MigrateAsync();
}
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();
public partial class Program { }