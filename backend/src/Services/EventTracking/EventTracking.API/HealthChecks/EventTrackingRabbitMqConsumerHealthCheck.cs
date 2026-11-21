using System.Threading;
using System.Threading.Tasks;
using EventTracking.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EventTracking.API.HealthChecks;

public sealed class EventTrackingRabbitMqConsumerHealthCheck(
    EventTrackingRabbitMqConsumerStatus status,
    EventTrackingRabbitMqConsumerOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Event Tracking RabbitMQ consumer is disabled."));
        return Task.FromResult(status.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Event Tracking RabbitMQ consumer is disconnected."));
    }
}