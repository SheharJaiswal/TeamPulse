using System;
using System.Threading;
using System.Threading.Tasks;
using Notification.Domain.Entities;
namespace Notification.Application.Abstractions;
public interface IEmployeeStatusProjectionReader
{
    Task<EmployeeStatusProjection?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken);
}