using Insight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insight.API.HealthChecks;

public sealed class InsightActivityDatabaseHealthCheck(
    IDbContextFactory<InsightActivityDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Insight activity database is unavailable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("Insight activity database is unavailable.");
        }
    }
}
