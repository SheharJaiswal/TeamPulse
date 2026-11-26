using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;
namespace Identity.Application.Abstractions;
public interface IEmployeeRepository
{
    Task<Employee?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Employee>> GetActiveByOrganizationAsync(string organizationId, CancellationToken cancellationToken);
    Task AddAsync(Employee employee, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}