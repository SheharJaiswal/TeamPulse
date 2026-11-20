using System;
using System.Threading;
using System.Threading.Tasks;
using Habit.Domain.Entities;

namespace Habit.Application.Abstractions;

public interface IEmployeeStatusProjectionReader
{
    Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);
}
