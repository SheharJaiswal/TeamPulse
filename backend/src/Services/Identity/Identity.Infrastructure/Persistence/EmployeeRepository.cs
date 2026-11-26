using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Identity.Infrastructure.Persistence;
public sealed class EmployeeRepository(IdentityDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByNormalizedEmailAsync(string email,CancellationToken ct)=>db.Employees.SingleOrDefaultAsync(x=>x.NormalizedEmail==email,ct);
    public Task<Employee?> GetByIdAsync(Guid id,CancellationToken ct)=>db.Employees.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<Employee>> GetActiveByOrganizationAsync(string organizationId,CancellationToken ct) =>
        await db.Employees.AsNoTracking()
            .Where(x=>x.OrganizationId==organizationId && x.IsActive && x.Role=="Employee")
            .OrderBy(x=>x.DisplayName)
            .ToListAsync(ct);
    public async Task AddAsync(Employee employee,CancellationToken ct)=>await db.Employees.AddAsync(employee,ct);
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
}