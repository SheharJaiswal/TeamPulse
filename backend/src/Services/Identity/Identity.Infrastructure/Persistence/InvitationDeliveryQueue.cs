using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Identity.Infrastructure.Persistence;

public sealed class InvitationDeliveryQueue(
    IDbContextFactory<IdentityDbContext> contextFactory,
    TimeProvider timeProvider) : IInvitationDeliveryQueue
{
    public async Task<IReadOnlyList<InvitationDeliveryWorkItem>> ClaimBatchAsync(
        int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        var secrets = await database.InvitationDeliverySecrets
            .FromSqlInterpolated($@"
SELECT s.* FROM invitation_delivery_secrets s
WHERE EXISTS (
    SELECT 1 FROM employee_invitations i
    WHERE i.Id = s.InvitationId AND i.Status = 'Pending' AND i.ExpiresAtUtc > {nowUtc}
  )
  AND (s.NextAttemptAtUtc IS NULL OR s.NextAttemptAtUtc <= {nowUtc})
  AND (s.LeaseExpiresAtUtc IS NULL OR s.LeaseExpiresAtUtc <= {nowUtc})
ORDER BY s.CreatedAtUtc, s.Id
LIMIT {batchSize}
FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var claimed = new List<InvitationDeliveryWorkItem>(secrets.Count);
        foreach (var secret in secrets)
        {
            var invitation = await database.EmployeeInvitations.SingleOrDefaultAsync(
                item => item.Id == secret.InvitationId, cancellationToken);
            if (invitation is null || invitation.Status != "Pending" ||
                invitation.ExpiresAtUtc <= nowUtc ||
                invitation.DeliveryGeneration != secret.Generation)
            {
                if (invitation is not null && invitation.Status == "Pending" &&
                    invitation.MarkExpired(nowUtc))
                {
                    // Expiry invalidates the digest as well as the delivery secret.
                }
                database.InvitationDeliverySecrets.Remove(secret);
                continue;
            }

            var leaseToken = Guid.NewGuid();
            if (!secret.TryAcquireLease(leaseToken, nowUtc, nowUtc.Add(leaseDuration)))
                continue;

            claimed.Add(new InvitationDeliveryWorkItem(
                secret.Id, invitation.Id, secret.Generation, secret.ProtectedToken,
                invitation.Email, invitation.DisplayName, invitation.ExpiresAtUtc,
                leaseToken, secret.AttemptCount));
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public async Task<bool> MarkAcceptedAsync(
        Guid secretId, int generation, Guid leaseToken, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await database.InvitationDeliverySecrets
            .Where(secret => secret.Id == secretId &&
                             secret.Generation == generation &&
                             secret.LeaseToken == leaseToken)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid secretId, int generation, Guid leaseToken, DateTime nextAttemptAtUtc,
        string errorCode, CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await database.InvitationDeliverySecrets.SingleOrDefaultAsync(
            item => item.Id == secretId && item.Generation == generation, cancellationToken);
        if (secret is null || !secret.TryScheduleRetry(leaseToken, nextAttemptAtUtc, errorCode))
            return false;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<int> ExpirePendingAsync(CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expired = await database.EmployeeInvitations
            .FromSqlInterpolated($@"
SELECT * FROM employee_invitations
WHERE Status = 'Pending' AND ExpiresAtUtc <= {nowUtc}
ORDER BY ExpiresAtUtc
LIMIT 100
FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        foreach (var invitation in expired)
        {
            invitation.MarkExpired(nowUtc);
            var secret = await database.InvitationDeliverySecrets.SingleOrDefaultAsync(
                item => item.InvitationId == invitation.Id, cancellationToken);
            if (secret is not null)
                database.InvitationDeliverySecrets.Remove(secret);
        }

        if (expired.Count > 0)
            await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expired.Count;
    }
}
