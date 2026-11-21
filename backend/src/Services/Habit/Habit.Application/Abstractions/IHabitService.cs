using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Contracts;
using Habit.Domain.Entities;

namespace Habit.Application.Abstractions;

public interface IHabitService
{
    Task<IReadOnlyList<HabitResponse>> GetMineAsync(Guid employeeId, HabitStatus? status, CancellationToken cancellationToken);
    Task<HabitResponse?> GetAsync(Guid employeeId, Guid habitId, CancellationToken cancellationToken);
    Task<HabitResponse> CreateAsync(Guid employeeId, CreateHabitRequest request, CancellationToken cancellationToken);
    Task<HabitResponse?> UpdateAsync(Guid employeeId, Guid habitId, UpdateHabitRequest request, CancellationToken cancellationToken);
    Task<bool> ArchiveAsync(Guid employeeId, Guid habitId, CancellationToken cancellationToken);
}
