using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Notification.Infrastructure.Messaging;
namespace Notification.API.HealthChecks;
public sealed class NotificationRabbitMqConsumerHealthCheck(
    NotificationRabbitMqConsumerStatus status,
    NotificationRabbitMqConsumerOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Notification RabbitMQ consumer is disabled."));
        return Task.FromResult(status.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Notification RabbitMQ consumer is disconnected."));
    }
}