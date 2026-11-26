using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Application.Abstractions;
using Challenge.Application.Contracts;

namespace Challenge.Application;

public sealed class EmployeeStatusProjectionProcessor(IEmployeeStatusProjectionStore store, TimeProvider timeProvider)
    : IEmployeeStatusProjectionProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    public async Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message)) return ProjectionProcessingOutcome.Invalid;
        EmployeeLifecycleEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<EmployeeLifecycleEnvelope>(message, JsonOptions); }
        catch (JsonException) { return ProjectionProcessingOutcome.Invalid; }

        if (envelope is null || !envelope.SchemaVersion.HasValue) return ProjectionProcessingOutcome.Invalid;
        if (envelope.SchemaVersion.Value != 1) return ProjectionProcessingOutcome.UnsupportedSchemaVersion;
        if (envelope.Payload is null) return ProjectionProcessingOutcome.Invalid;
        if (!string.Equals(envelope.Payload.Source, "hr-admin-api", StringComparison.Ordinal))
            return ProjectionProcessingOutcome.InvalidProducer;

        if (!TryMap(envelope, out var lifecycleEvent)) return ProjectionProcessingOutcome.Invalid;
        return await store.ProcessAsync(lifecycleEvent, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    private static bool TryMap(EmployeeLifecycleEnvelope envelope, out EmployeeLifecycleEvent lifecycleEvent)
    {
        lifecycleEvent = default!;
        if (envelope.EventId == Guid.Empty || envelope.CorrelationId == Guid.Empty || envelope.SchemaVersion != 1 ||
            envelope.OccurredAtUtc == default || envelope.OccurredAtUtc.Offset != TimeSpan.Zero || envelope.Payload is null)
            return false;

        var payload = envelope.Payload;
        if (payload.EmployeeId == Guid.Empty || payload.ActorEmployeeId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.ReasonCode) || payload.ReasonCode.Length > 100 ||
            payload.LifecycleVersion <= 0)
            return false;

        bool isActive;
        if (envelope.EventType == "EmployeeDeactivated" && payload.PreviousStatus == "active" && payload.NewStatus == "inactive")
            isActive = false;
        else if (envelope.EventType == "EmployeeReactivated" && payload.PreviousStatus == "inactive" && payload.NewStatus == "active")
            isActive = true;
        else if (envelope.EventType == "EmployeeActivated" && payload.PreviousStatus == "pending" && payload.NewStatus == "active")
            isActive = true;
        else if (envelope.EventType == "EmployeeActiveSnapshot" && payload.PreviousStatus == "active" && payload.NewStatus == "active")
            isActive = true;
        else return false;

        var fingerprint = LifecycleEventFingerprint.Create(envelope, payload);
        lifecycleEvent = new EmployeeLifecycleEvent(
            envelope.EventId, envelope.EventType, envelope.SchemaVersion.Value, payload.EmployeeId, isActive,
            payload.LifecycleVersion, envelope.CorrelationId, envelope.OccurredAtUtc.UtcDateTime,
            payload.ActorEmployeeId, payload.Source, payload.PreviousStatus, payload.NewStatus,
            payload.ReasonCode, LifecycleEventFingerprint.Algorithm, fingerprint);
        return true;
    }
}
