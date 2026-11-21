using System.Threading;
using System.Threading.Tasks;
using Notification.Application.Contracts;
namespace Notification.Application.Abstractions;
public interface IEmployeeStatusProjectionProcessor
{
    Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken);
}