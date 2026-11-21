using Insight.Application.Contracts;

namespace Insight.Application.Abstractions;

public interface IEmployeeStatusProjectionStore
{
    Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken);
}