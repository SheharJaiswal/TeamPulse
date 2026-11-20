using System;
using System.Threading;
using System.Threading.Tasks;
using Habit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Habit.API.HealthChecks;

public sealed class HabitDatabaseHealthCheck(
    IDbContextFactory<HabitDbContext> contextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Habit database is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Habit database health check timed out.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Habit database is unavailable.");
        }
    }
}
