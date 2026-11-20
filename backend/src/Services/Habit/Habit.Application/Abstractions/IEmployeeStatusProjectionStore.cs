using System;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Contracts;

namespace Habit.Application.Abstractions;

public interface IEmployeeStatusProjectionStore : IEmployeeStatusProjectionReader
{
    Task<ProjectionProcessingOutcome> ProcessAsync(
        EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken);
}
