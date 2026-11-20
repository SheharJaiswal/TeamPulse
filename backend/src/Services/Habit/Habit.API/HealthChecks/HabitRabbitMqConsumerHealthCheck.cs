using System.Threading;
using System.Threading.Tasks;
using Habit.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Habit.API.HealthChecks;

public sealed class HabitRabbitMqConsumerHealthCheck(
    HabitRabbitMqConsumerStatus status,
    HabitRabbitMqConsumerOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Habit RabbitMQ consumer is disabled."));

        return Task.FromResult(status.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Habit RabbitMQ consumer is disconnected."));
    }
}
