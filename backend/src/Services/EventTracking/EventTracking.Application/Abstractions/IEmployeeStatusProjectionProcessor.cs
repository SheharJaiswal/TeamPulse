using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Contracts;
namespace EventTracking.Application.Abstractions;
public interface IEmployeeStatusProjectionProcessor { Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken); }