using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Domain.Entities;

namespace EventTracking.Application.Abstractions;

public interface IChallengeActivitySubmissionStore
{
    Task<ChallengeActivitySubmission?> GetByIdempotencyKeyAsync(Guid employeeId, Guid key, CancellationToken cancellationToken);
    Task<ChallengeActivitySubmission> AddAsync(ChallengeActivitySubmission submission, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChallengeActivitySubmission>> GetMineAsync(Guid employeeId, Guid challengeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChallengeActivitySubmission>> GetForEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, Guid challengeId, CancellationToken cancellationToken);
}
