using System;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Challenge.API.HealthChecks;

public sealed class ChallengeDatabaseHealthCheck(IDbContextFactory<ChallengeDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Challenge database is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Challenge database health check timed out.");
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("Challenge database is unavailable."); }
    }
}