using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Habit.Domain.Entities;

namespace Habit.Application.Abstractions;

public interface IHabitRepository
{
    Task<IReadOnlyList<HabitDefinition>> GetByOwnerAsync(Guid ownerEmployeeId, HabitStatus? status, CancellationToken cancellationToken);
    Task<HabitDefinition?> GetByIdAndOwnerAsync(Guid id, Guid ownerEmployeeId, CancellationToken cancellationToken);
    Task AddAsync(HabitDefinition habit, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
