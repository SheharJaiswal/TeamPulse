using Insight.Application.Contracts;

namespace Insight.Application.Abstractions;

public interface IEmployeeStatusProjectionProcessor
{
    Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken);
}