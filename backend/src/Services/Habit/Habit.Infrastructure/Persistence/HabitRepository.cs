using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Habit.Infrastructure.Persistence;

public sealed class HabitRepository(HabitDbContext database) : IHabitRepository
{
    public async Task<IReadOnlyList<HabitDefinition>> GetByOwnerAsync(
        Guid ownerEmployeeId, HabitStatus? status, CancellationToken cancellationToken)
    {
        var query = database.Habits.AsNoTracking().Where(x => x.OwnerEmployeeId == ownerEmployeeId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<HabitDefinition?> GetByIdAndOwnerAsync(
        Guid id, Guid ownerEmployeeId, CancellationToken cancellationToken) =>
        database.Habits.SingleOrDefaultAsync(
            x => x.Id == id && x.OwnerEmployeeId == ownerEmployeeId, cancellationToken);

    public async Task AddAsync(HabitDefinition habit, CancellationToken cancellationToken) =>
        await database.Habits.AddAsync(habit, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        database.SaveChangesAsync(cancellationToken);
}
