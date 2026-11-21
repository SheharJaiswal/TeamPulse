using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Abstractions;

public sealed record InvitationDeliveryWorkItem(
    Guid SecretId,
    Guid InvitationId,
    int Generation,
    string ProtectedToken,
    string Email,
    string DisplayName,
    DateTime ExpiresAtUtc,
    Guid LeaseToken,
    int AttemptCount);

public interface IInvitationDeliveryQueue
{
    Task<IReadOnlyList<InvitationDeliveryWorkItem>> ClaimBatchAsync(
        int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task<bool> MarkAcceptedAsync(
        Guid secretId, int generation, Guid leaseToken, CancellationToken cancellationToken);

    Task<bool> ScheduleRetryAsync(
        Guid secretId, int generation, Guid leaseToken, DateTime nextAttemptAtUtc,
        string errorCode, CancellationToken cancellationToken);

    Task<int> ExpirePendingAsync(CancellationToken cancellationToken);
}

public interface IInvitationEmailSender
{
    Task SendAsync(string email, string displayName, string activationUrl,
        CancellationToken cancellationToken);
}
