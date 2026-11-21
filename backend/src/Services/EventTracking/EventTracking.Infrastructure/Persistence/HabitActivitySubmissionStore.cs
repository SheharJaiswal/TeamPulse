using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace EventTracking.Infrastructure.Persistence;

public sealed class HabitActivitySubmissionStore(IDbContextFactory<EventTrackingDbContext> contextFactory) : IHabitActivitySubmissionStore
{
    public async Task<HabitActivitySubmission?> GetByIdempotencyKeyAsync(Guid employeeId, Guid key, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.HabitActivitySubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IdempotencyKey == key, cancellationToken);
    }

    public async Task<HabitActivitySubmission> AddAsync(HabitActivitySubmission submission, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        database.HabitActivitySubmissions.Add(submission);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return submission;
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException mysql && mysql.Number == 1062)
        {
            var existing = await database.HabitActivitySubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EmployeeId == submission.EmployeeId && x.IdempotencyKey == submission.IdempotencyKey, cancellationToken);
            if (existing is not null) return existing;
            throw;
        }
    }

    public async Task<IReadOnlyList<HabitActivitySubmission>> GetMineAsync(Guid employeeId, Guid? habitId,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = database.HabitActivitySubmissions.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        if (habitId.HasValue) query = query.Where(x => x.HabitId == habitId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.ActivityDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.ActivityDate <= toDate.Value);
        return await query.OrderByDescending(x => x.ActivityDate).ThenByDescending(x => x.SubmittedAtUtc)
            .ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<HabitActivitySubmission?> GetAsync(Guid employeeId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.HabitActivitySubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == submissionId, cancellationToken);
    }
}
