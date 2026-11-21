using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insight.API.HealthChecks;

public sealed class InsightConsumerHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Consumer registration is introduced in the next implementation increment.
        return Task.FromResult(HealthCheckResult.Healthy("Insight RabbitMQ consumer is not enabled in this host increment."));
    }
}