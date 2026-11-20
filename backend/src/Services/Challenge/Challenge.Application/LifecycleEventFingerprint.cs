using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Challenge.Application.Contracts;

namespace Challenge.Application;

internal static class LifecycleEventFingerprint
{
    public const string Algorithm = "rfc8785-sha256-v1";
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Create(EmployeeLifecycleEnvelope envelope, EmployeeLifecyclePayload payload)
    {
        var occurredAtUtc = envelope.OccurredAtUtc.ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

        // All keys are emitted in RFC 8785 UTF-16 ordinal order. All values are strings
        // except schemaVersion; lifecycleVersion is a decimal string to preserve full int64.
        var canonical = new StringBuilder(768)
            .Append('{')
            .Append(Quote("actorEmployeeId")).Append(':').Append(Quote(payload.ActorEmployeeId.ToString("D").ToLowerInvariant())).Append(',')
            .Append(Quote("correlationId")).Append(':').Append(Quote(envelope.CorrelationId.ToString("D").ToLowerInvariant())).Append(',')
            .Append(Quote("employeeId")).Append(':').Append(Quote(payload.EmployeeId.ToString("D").ToLowerInvariant())).Append(',')
            .Append(Quote("eventId")).Append(':').Append(Quote(envelope.EventId.ToString("D").ToLowerInvariant())).Append(',')
            .Append(Quote("eventType")).Append(':').Append(Quote(envelope.EventType)).Append(',')
            .Append(Quote("lifecycleVersion")).Append(':').Append(Quote(payload.LifecycleVersion.ToString(CultureInfo.InvariantCulture))).Append(',')
            .Append(Quote("newStatus")).Append(':').Append(Quote(payload.NewStatus)).Append(',')
            .Append(Quote("occurredAtUtc")).Append(':').Append(Quote(occurredAtUtc)).Append(',')
            .Append(Quote("previousStatus")).Append(':').Append(Quote(payload.PreviousStatus)).Append(',')
            .Append(Quote("reasonCode")).Append(':').Append(Quote(payload.ReasonCode)).Append(',')
            .Append(Quote("schemaVersion")).Append(':').Append(envelope.SchemaVersion!.Value.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(Quote("source")).Append(':').Append(Quote(payload.Source))
            .Append('}')
            .ToString();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value, CanonicalJsonOptions);
}
