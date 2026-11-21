using Insight.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insight.API.HealthChecks;

public sealed class InsightConsumerHealthCheck : IHealthCheck
{
    private readonly InsightRabbitMqConsumerStatus _status;
    private readonly InsightRabbitMqConsumerOptions _options;

    public InsightConsumerHealthCheck(InsightRabbitMqConsumerStatus status, InsightRabbitMqConsumerOptions options)
    {
        _status = status;
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Insight RabbitMQ consumer is disabled."));
        return Task.FromResult(_status.IsConnected
            ? HealthCheckResult.Healthy("Insight RabbitMQ consumer is connected.")
            : HealthCheckResult.Unhealthy("Insight RabbitMQ consumer is not connected."));
    }
}