using System;
using System.Linq;

namespace Identity.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid eventId, string eventType, int schemaVersion,
        DateTime occurredAtUtc, Guid correlationId, string payload)
    {
        EventId = eventId;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId;
        Payload = payload;
        CreatedAtUtc = DateTime.UtcNow;
        AttemptCount = 0;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? DeadLetteredAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }

    public bool TryAcquireLease(Guid leaseToken, DateTime nowUtc, DateTime leaseExpiresAtUtc)
    {
        if (leaseToken == Guid.Empty || PublishedAtUtc.HasValue || DeadLetteredAtUtc.HasValue)
            return false;
        if (NextAttemptAtUtc.HasValue && NextAttemptAtUtc.Value > nowUtc)
            return false;
        if (LeaseExpiresAtUtc.HasValue && LeaseExpiresAtUtc.Value > nowUtc)
            return false;
        LeaseToken = leaseToken;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
        LastAttemptAtUtc = nowUtc;
        AttemptCount = checked(AttemptCount + 1);
        return true;
    }

    public bool TryMarkPublished(Guid leaseToken, DateTime publishedAtUtc)
    {
        if (!OwnsLease(leaseToken))
            return false;
        PublishedAtUtc = publishedAtUtc;
        ClearLease();
        NextAttemptAtUtc = null;
        LastErrorCode = null;
        return true;
    }

    public bool TryScheduleRetry(Guid leaseToken, DateTime nextAttemptAtUtc, string errorCode)
    {
        if (!OwnsLease(leaseToken))
            return false;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LastErrorCode = SanitizeErrorCode(errorCode);
        ClearLease();
        return true;
    }

    public bool TryQuarantine(Guid leaseToken, DateTime deadLetteredAtUtc, string errorCode)
    {
        if (!OwnsLease(leaseToken))
            return false;
        DeadLetteredAtUtc = deadLetteredAtUtc;
        NextAttemptAtUtc = null;
        LastErrorCode = SanitizeErrorCode(errorCode);
        ClearLease();
        return true;
    }

    private bool OwnsLease(Guid leaseToken) =>
        leaseToken != Guid.Empty && LeaseToken == leaseToken &&
        !PublishedAtUtc.HasValue && !DeadLetteredAtUtc.HasValue;

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
