using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Contracts;

namespace Challenge.Application.Abstractions;

public interface IEmployeeStatusProjectionProcessor
{
    Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken);
}