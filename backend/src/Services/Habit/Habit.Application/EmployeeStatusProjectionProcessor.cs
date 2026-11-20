using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Application.Contracts;

namespace Habit.Application;

public sealed class EmployeeStatusProjectionProcessor : IEmployeeStatusProjectionProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    private readonly IEmployeeStatusProjectionStore _store;
    private readonly TimeProvider _timeProvider;

    public EmployeeStatusProjectionProcessor(
        IEmployeeStatusProjectionStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ProjectionProcessingOutcome.Invalid;

        EmployeeLifecycleEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EmployeeLifecycleEnvelope>(message, JsonOptions);
        }
        catch (JsonException)
        {
            return ProjectionProcessingOutcome.Invalid;
        }

        if (envelope is null || !TryMap(envelope, out var lifecycleEvent))
            return ProjectionProcessingOutcome.Invalid;

        return await _store.ProcessAsync(
            lifecycleEvent,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    private static bool TryMap(
        EmployeeLifecycleEnvelope envelope, out EmployeeLifecycleEvent lifecycleEvent)
    {
        lifecycleEvent = default!;

        if (envelope.EventId == Guid.Empty ||
            envelope.CorrelationId == Guid.Empty ||
            envelope.SchemaVersion != 1 ||
            envelope.OccurredAtUtc == default ||
            envelope.OccurredAtUtc.Offset != TimeSpan.Zero ||
            envelope.Payload is null)
            return false;

        var payload = envelope.Payload;
        if (payload.EmployeeId == Guid.Empty ||
            payload.ActorEmployeeId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.Source) ||
            payload.Source.Length > 50 ||
            string.IsNullOrWhiteSpace(payload.ReasonCode) ||
            payload.ReasonCode.Length > 100 ||
            payload.LifecycleVersion <= 0)
            return false;

        bool isActive;
        if (string.Equals(envelope.EventType, "EmployeeDeactivated", StringComparison.Ordinal) &&
            string.Equals(payload.PreviousStatus, "active", StringComparison.Ordinal) &&
            string.Equals(payload.NewStatus, "inactive", StringComparison.Ordinal))
        {
            isActive = false;
        }
        else if (string.Equals(envelope.EventType, "EmployeeReactivated", StringComparison.Ordinal) &&
                 string.Equals(payload.PreviousStatus, "inactive", StringComparison.Ordinal) &&
                 string.Equals(payload.NewStatus, "active", StringComparison.Ordinal))
        {
            isActive = true;
        }
        else
        {
            return false;
        }

        lifecycleEvent = new EmployeeLifecycleEvent(
            envelope.EventId,
            envelope.EventType,
            envelope.SchemaVersion,
            payload.EmployeeId,
            isActive,
            payload.LifecycleVersion,
            envelope.CorrelationId,
            envelope.OccurredAtUtc.UtcDateTime);

        return true;
    }
}
