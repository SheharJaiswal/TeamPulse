using System;
using System.Threading;
using System.Threading.Tasks;
using Notification.Application.Contracts;
namespace Notification.Application.Abstractions;
public interface IEmployeeStatusProjectionStore
{
    Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken);
}