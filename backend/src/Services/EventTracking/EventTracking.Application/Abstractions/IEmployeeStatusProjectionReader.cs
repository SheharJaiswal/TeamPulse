using System;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Domain.Entities;
namespace EventTracking.Application.Abstractions;
public interface IEmployeeStatusProjectionReader { Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken); }