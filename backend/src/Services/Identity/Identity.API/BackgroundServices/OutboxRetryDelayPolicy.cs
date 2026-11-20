using System;

namespace Identity.API.BackgroundServices;

public static class OutboxRetryDelayPolicy
{
    public static TimeSpan CalculateDelay(
        int attemptCount,
        int baseDelaySeconds,
        int maxDelaySeconds,
        double jitterSample)
    {
        if (attemptCount < 1)
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        if (baseDelaySeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(baseDelaySeconds));
        if (maxDelaySeconds < baseDelaySeconds)
            throw new ArgumentOutOfRangeException(nameof(maxDelaySeconds));
        if (double.IsNaN(jitterSample) || jitterSample < 0 || jitterSample >= 1)
            throw new ArgumentOutOfRangeException(nameof(jitterSample));

        var exponent = Math.Min(attemptCount - 1, 20);
        var ceilingSeconds = Math.Min(
            baseDelaySeconds * Math.Pow(2, exponent),
            maxDelaySeconds);

        return TimeSpan.FromSeconds(jitterSample * ceilingSeconds);
    }
}
