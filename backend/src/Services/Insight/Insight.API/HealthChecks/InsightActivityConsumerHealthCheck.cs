using Insight.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insight.API.HealthChecks;

public sealed class InsightActivityConsumerHealthCheck(
    InsightActivityConsumerStatus status,
    InsightActivityConsumerOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Insight activity consumer is disabled."));
        return Task.FromResult(status.IsConnected
            ? HealthCheckResult.Healthy("Insight activity consumer is connected.")
            : HealthCheckResult.Unhealthy("Insight activity consumer is not connected."));
    }
}
