using System;
using System.Linq;
namespace EventTracking.Domain.Entities;
public sealed class EventTrackingOutboxMessage
{
    private EventTrackingOutboxMessage() { }
    public EventTrackingOutboxMessage(Guid eventId, string eventType, int schemaVersion, DateTime occurredAtUtc, Guid correlationId, string payload)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 100) throw new ArgumentException("Event type is invalid.", nameof(eventType));
        if (schemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (occurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Occurrence timestamp must be UTC.", nameof(occurredAtUtc));
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));
        EventId = eventId; EventType = eventType; SchemaVersion = schemaVersion; OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId; Payload = payload; CreatedAtUtc = occurredAtUtc;
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
    public DateTime? DeadLetteredAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public bool TryAcquireLease(Guid token, DateTime nowUtc, DateTime expiresUtc)
    {
        if (token == Guid.Empty || !IsUtc(nowUtc) || !IsUtc(expiresUtc) || expiresUtc <= nowUtc ||
            PublishedAtUtc.HasValue || DeadLetteredAtUtc.HasValue ||
            (NextAttemptAtUtc.HasValue && NextAttemptAtUtc.Value > nowUtc) ||
            (LeaseExpiresAtUtc.HasValue && LeaseExpiresAtUtc.Value > nowUtc)) return false;
        LeaseToken = token; LeaseExpiresAtUtc = expiresUtc; AttemptCount = checked(AttemptCount + 1); return true;
    }
    public bool TryMarkPublished(Guid token, DateTime publishedAtUtc)
    {
        if (!OwnsUnexpiredLease(token, publishedAtUtc)) return false;
        PublishedAtUtc = publishedAtUtc; ClearLease(); NextAttemptAtUtc = null; LastErrorCode = null; return true;
    }
    public bool TryScheduleRetry(Guid token, DateTime attemptedAtUtc, DateTime retryAtUtc, string errorCode)
    {
        if (!IsUtc(retryAtUtc) || !OwnsUnexpiredLease(token, attemptedAtUtc)) return false;
        NextAttemptAtUtc = retryAtUtc; LastErrorCode = Sanitize(errorCode); ClearLease(); return true;
    }
    public bool TryQuarantine(Guid token, DateTime deadLetteredAtUtc, string errorCode)
    {
        if (!IsUtc(deadLetteredAtUtc) || !OwnsUnexpiredLease(token, deadLetteredAtUtc)) return false;
        DeadLetteredAtUtc = deadLetteredAtUtc; LastErrorCode = Sanitize(errorCode); ClearLease(); return true;
    }
    private bool OwnsUnexpiredLease(Guid token, DateTime transitionAtUtc) =>
        IsUtc(transitionAtUtc) && token != Guid.Empty && LeaseToken == token &&
        LeaseExpiresAtUtc.HasValue && LeaseExpiresAtUtc.Value > transitionAtUtc &&
        !PublishedAtUtc.HasValue && !DeadLetteredAtUtc.HasValue;
    private static bool IsUtc(DateTime value) => value.Kind == DateTimeKind.Utc;
    private void ClearLease() { LeaseToken = null; LeaseExpiresAtUtc = null; }
    private static string Sanitize(string value)
    {
        var safe = new string((value ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').Take(100).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }
}
