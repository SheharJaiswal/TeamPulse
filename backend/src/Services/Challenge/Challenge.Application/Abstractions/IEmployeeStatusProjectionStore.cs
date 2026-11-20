using System;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Contracts;
using Challenge.Domain.Entities;

namespace Challenge.Application.Abstractions;

public interface IEmployeeStatusProjectionStore
{
    Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken);
    Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);
}