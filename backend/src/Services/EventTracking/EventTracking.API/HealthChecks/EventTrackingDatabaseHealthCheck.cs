using System;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace EventTracking.API.HealthChecks;
public sealed class EventTrackingDatabaseHealthCheck(IDbContextFactory<EventTrackingDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Event Tracking database is unavailable.");
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("Event Tracking database is unavailable."); }
    }
}