using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Contracts;

namespace Habit.Application.Abstractions;

public interface IEmployeeStatusProjectionProcessor
{
    Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken);
}
