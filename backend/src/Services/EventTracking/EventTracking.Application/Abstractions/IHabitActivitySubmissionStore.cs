using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Domain.Entities;

namespace EventTracking.Application.Abstractions;

public interface IHabitActivitySubmissionStore
{
    Task<HabitActivitySubmission?> GetByIdempotencyKeyAsync(Guid employeeId, Guid key, CancellationToken cancellationToken);
    Task<HabitActivitySubmission> AddAsync(HabitActivitySubmission submission, CancellationToken cancellationToken);
    Task<IReadOnlyList<HabitActivitySubmission>> GetMineAsync(Guid employeeId, Guid? habitId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<HabitActivitySubmission?> GetAsync(Guid employeeId, Guid submissionId, CancellationToken cancellationToken);
}
