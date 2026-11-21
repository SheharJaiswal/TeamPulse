using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
namespace EventTracking.Application;
public sealed class EmployeeStatusProjectionProcessor : IEmployeeStatusProjectionProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, NumberHandling = JsonNumberHandling.Strict };
    private readonly IEmployeeStatusProjectionStore _store;
    private readonly TimeProvider _timeProvider;
    public EmployeeStatusProjectionProcessor(IEmployeeStatusProjectionStore store, TimeProvider timeProvider)
    { _store = store; _timeProvider = timeProvider; }
    public async Task<ProjectionProcessingOutcome> ProcessAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message)) return ProjectionProcessingOutcome.Invalid;
        EmployeeLifecycleEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<EmployeeLifecycleEnvelope>(message, JsonOptions); }
        catch (JsonException) { return ProjectionProcessingOutcome.Invalid; }
        if (envelope is null || !envelope.SchemaVersion.HasValue) return ProjectionProcessingOutcome.Invalid;
        if (envelope.SchemaVersion.Value != 1) return ProjectionProcessingOutcome.UnsupportedSchemaVersion;
        if (envelope.Payload is null) return ProjectionProcessingOutcome.Invalid;
        var payload = envelope.Payload;
        var reasonCode = payload.ReasonCode?.Trim();
        if (envelope.EventId == Guid.Empty || envelope.CorrelationId == Guid.Empty ||
            envelope.OccurredAtUtc == default || envelope.OccurredAtUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrEmpty(envelope.EventType) || payload.EmployeeId == Guid.Empty ||
            payload.ActorEmployeeId == Guid.Empty ||
            !string.Equals(payload.Source, "hr-admin-api", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(reasonCode) || reasonCode.Length > 100 || payload.LifecycleVersion <= 0)
            return ProjectionProcessingOutcome.Invalid;
        bool isActive;
        if (envelope.EventType == "EmployeeDeactivated" && payload.PreviousStatus == "active" && payload.NewStatus == "inactive")
            isActive = false;
        else if (envelope.EventType == "EmployeeReactivated" && payload.PreviousStatus == "inactive" && payload.NewStatus == "active")
            isActive = true;
        else return ProjectionProcessingOutcome.Invalid;
        var lifecycleEvent = new EmployeeLifecycleEvent(envelope.EventId, envelope.EventType, 1, payload.EmployeeId,
            isActive, payload.LifecycleVersion, envelope.CorrelationId, envelope.OccurredAtUtc.UtcDateTime);
        return await _store.ProcessAsync(lifecycleEvent, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}