using System.Threading;
using System.Threading.Tasks;
using Challenge.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Challenge.API.HealthChecks;

public sealed class ChallengeRabbitMqConsumerHealthCheck(
    ChallengeRabbitMqConsumerStatus status, ChallengeRabbitMqConsumerOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return Task.FromResult(HealthCheckResult.Healthy("Challenge RabbitMQ consumer is disabled."));
        return Task.FromResult(status.IsConnected ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Challenge RabbitMQ consumer is disconnected."));
    }
}