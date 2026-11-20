using System;

namespace Identity.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(
        Guid eventId, string eventType, int schemaVersion,
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
}
