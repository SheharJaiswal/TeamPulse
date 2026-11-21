using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.Infrastructure.Persistence;
namespace Notification.API.HealthChecks;
public sealed class NotificationDatabaseHealthCheck(IDbContextFactory<NotificationDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Notification database is unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Notification database is unavailable.");
        }
    }
}