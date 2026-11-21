using System;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Contracts;
namespace EventTracking.Application.Abstractions;
public interface IEmployeeStatusProjectionStore { Task<ProjectionProcessingOutcome> ProcessAsync(EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken); }