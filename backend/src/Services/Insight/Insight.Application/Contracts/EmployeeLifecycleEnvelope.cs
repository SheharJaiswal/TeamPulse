using System.Text.Json.Serialization;

namespace Insight.Application.Contracts;

public sealed record EmployeeLifecycleEnvelope
{
    [JsonPropertyName("eventId")] public Guid EventId { get; init; }
    [JsonPropertyName("eventType")] public string EventType { get; init; } = string.Empty;
    [JsonPropertyName("schemaVersion")] public int? SchemaVersion { get; init; }
    [JsonPropertyName("occurredAtUtc")] public DateTimeOffset OccurredAtUtc { get; init; }
    [JsonPropertyName("correlationId")] public Guid CorrelationId { get; init; }
    [JsonPropertyName("payload")] public EmployeeLifecyclePayload? Payload { get; init; }
}
public sealed record EmployeeLifecyclePayload
{
    [JsonPropertyName("employeeId")] public Guid EmployeeId { get; init; }
    [JsonPropertyName("actorEmployeeId")] public Guid ActorEmployeeId { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("previousStatus")] public string PreviousStatus { get; init; } = string.Empty;
    [JsonPropertyName("newStatus")] public string NewStatus { get; init; } = string.Empty;
    [JsonPropertyName("lifecycleVersion")] public long LifecycleVersion { get; init; }
    [JsonPropertyName("reasonCode")] public string ReasonCode { get; init; } = string.Empty;
    // Contract compatibility only; never copied to persistence or logs.
    [JsonPropertyName("note")] public string? Note { get; init; }
}