using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
using EventTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace EventTracking.Infrastructure.Persistence;

public sealed class ChallengeActivitySubmissionStore(
    IDbContextFactory<EventTrackingDbContext> contextFactory,
    TimeProvider timeProvider) : IChallengeActivitySubmissionStore
{
    public async Task<ChallengeActivitySubmission?> GetByIdempotencyKeyAsync(Guid employeeId, Guid key, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.ChallengeActivitySubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IdempotencyKey == key, cancellationToken);
    }

    public async Task<ChallengeActivitySubmission> AddAsync(ChallengeActivitySubmission submission, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        occurredAtUtc = new DateTime(occurredAtUtc.Ticks - occurredAtUtc.Ticks % 10, DateTimeKind.Utc);
        var payload = JsonSerializer.Serialize(new ChallengeActivityRecordedEvent(eventId, "ChallengeActivityRecorded", 1,
            occurredAtUtc, correlationId, new ChallengeActivityRecordedPayload(submission.EmployeeId, submission.Id,
                submission.ChallengeId, submission.ActivityDate, submission.Quantity)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        database.ChallengeActivitySubmissions.Add(submission);
        database.EventTrackingOutboxMessages.Add(new EventTrackingOutboxMessage(eventId, "ChallengeActivityRecorded", 1,
            occurredAtUtc, correlationId, payload));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return submission;
        }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException mysql && mysql.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken);
            await using var lookup = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await lookup.ChallengeActivitySubmissions.AsNoTracking().SingleOrDefaultAsync(
                x => x.EmployeeId == submission.EmployeeId && x.IdempotencyKey == submission.IdempotencyKey, cancellationToken);
            if (existing is not null) return existing;
            throw;
        }
    }

    public async Task<IReadOnlyList<ChallengeActivitySubmission>> GetMineAsync(Guid employeeId, Guid challengeId, CancellationToken cancellationToken) =>
        await GetForEmployeesAsync(new[] { employeeId }, challengeId, cancellationToken);

    public async Task<IReadOnlyList<ChallengeActivitySubmission>> GetForEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, Guid challengeId, CancellationToken cancellationToken)
    {
        if (employeeIds is null || employeeIds.Count == 0) return Array.Empty<ChallengeActivitySubmission>();
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.ChallengeActivitySubmissions.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.ChallengeId == challengeId)
            .OrderByDescending(x => x.ActivityDate).ThenByDescending(x => x.SubmittedAtUtc).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
