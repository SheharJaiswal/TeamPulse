using System;
using System.Linq;

namespace Identity.Domain.Entities;

public sealed class InvitationDeliverySecret
{
    private InvitationDeliverySecret() { }

    public InvitationDeliverySecret(Guid id, Guid invitationId, int generation, string protectedToken, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(protectedToken)) throw new ArgumentException("Protected token is required.", nameof(protectedToken));
        Id = id;
        InvitationId = invitationId;
        Generation = generation;
        ProtectedToken = protectedToken;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid InvitationId { get; private set; }
    public int Generation { get; private set; }
    public string ProtectedToken { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }

    public bool TryAcquireLease(Guid leaseToken, DateTime nowUtc, DateTime leaseExpiresAtUtc)
    {
        if (leaseToken == Guid.Empty ||
            (NextAttemptAtUtc.HasValue && NextAttemptAtUtc.Value > nowUtc) ||
            (LeaseExpiresAtUtc.HasValue && LeaseExpiresAtUtc.Value > nowUtc))
            return false;
        LeaseToken = leaseToken;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        AttemptCount = checked(AttemptCount + 1);
        return true;
    }

    public bool TryScheduleRetry(Guid leaseToken, DateTime nextAttemptAtUtc, string errorCode)
    {
        if (!OwnsLease(leaseToken)) return false;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastErrorCode = SanitizeErrorCode(errorCode);
        ClearLease();
        return true;
    }

    public void Replace(int generation, string protectedToken, DateTime createdAtUtc)
    {
        if (generation <= Generation)
            throw new ArgumentOutOfRangeException(nameof(generation), "Delivery generation must increase.");
        if (string.IsNullOrWhiteSpace(protectedToken))
            throw new ArgumentException("Protected token is required.", nameof(protectedToken));

        Generation = generation;
        ProtectedToken = protectedToken;
        CreatedAtUtc = createdAtUtc;
        AttemptCount = 0;
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        ClearLease();
    }

    private bool OwnsLease(Guid leaseToken) =>
        leaseToken != Guid.Empty && LeaseToken == leaseToken;

    private void ClearLease()
    {
        LeaseToken = null;
        LeaseExpiresAtUtc = null;
    }

    private static string SanitizeErrorCode(string errorCode)
    {
        var safe = new string((errorCode ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '-')
            .Take(100).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }
}
