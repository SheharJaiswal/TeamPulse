using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Application.Contracts;
using Habit.Domain.Entities;

namespace Habit.Application;

public sealed class HabitService(
    IHabitRepository repository,
    IEmployeeStatusProjectionReader employeeStatus,
    TimeProvider timeProvider) : IHabitService
{
    public async Task<IReadOnlyList<HabitResponse>> GetMineAsync(
        Guid employeeId, HabitStatus? status, CancellationToken cancellationToken)
    {
        await EnsureEmployeeActiveAsync(employeeId, cancellationToken);
        var habits = await repository.GetByOwnerAsync(employeeId, status, cancellationToken);
        return habits.Select(ToResponse).ToArray();
    }

    public async Task<HabitResponse?> GetAsync(Guid employeeId, Guid habitId, CancellationToken cancellationToken)
    {
        await EnsureEmployeeActiveAsync(employeeId, cancellationToken);
        var habit = await repository.GetByIdAndOwnerAsync(habitId, employeeId, cancellationToken);
        return habit is null ? null : ToResponse(habit);
    }

    public async Task<HabitResponse> CreateAsync(
        Guid employeeId, CreateHabitRequest request, CancellationToken cancellationToken)
    {
        await EnsureEmployeeActiveAsync(employeeId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var habit = new HabitDefinition(
            Guid.NewGuid(), employeeId, request.Name, request.Description, request.Category,
            request.Frequency, request.TargetCount, request.StartDate, request.EndDate, now);
        await repository.AddAsync(habit, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResponse(habit);
    }

    public async Task<HabitResponse?> UpdateAsync(
        Guid employeeId, Guid habitId, UpdateHabitRequest request, CancellationToken cancellationToken)
    {
        await EnsureEmployeeActiveAsync(employeeId, cancellationToken);
        var habit = await repository.GetByIdAndOwnerAsync(habitId, employeeId, cancellationToken);
        if (habit is null) return null;
        habit.Update(
            request.Name, request.Description, request.Category, request.Frequency,
            request.TargetCount, request.StartDate, request.EndDate,
            timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResponse(habit);
    }

    public async Task<bool> ArchiveAsync(Guid employeeId, Guid habitId, CancellationToken cancellationToken)
    {
        await EnsureEmployeeActiveAsync(employeeId, cancellationToken);
        var habit = await repository.GetByIdAndOwnerAsync(habitId, employeeId, cancellationToken);
        if (habit is null) return false;
        habit.Archive(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureEmployeeActiveAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var projection = await employeeStatus.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (projection is { IsActive: false })
            throw new UnauthorizedAccessException("Inactive employees cannot access Habit resources.");
    }

    private static HabitResponse ToResponse(HabitDefinition habit) =>
        new(habit.Id, habit.Name, habit.Description, habit.Category, habit.Frequency,
            habit.TargetCount, habit.StartDate, habit.EndDate, habit.Status,
            habit.CreatedAtUtc, habit.UpdatedAtUtc, habit.ArchivedAtUtc);
}
