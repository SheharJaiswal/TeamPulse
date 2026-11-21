using System;

namespace EventTracking.Infrastructure.Messaging;

public static class EventTrackingRetryDelayPolicy
{
    public static TimeSpan GetDelay(int failedAttempt, int baseDelayMilliseconds, int maxDelayMilliseconds, double jitterSample)
    {
        if (failedAttempt < 1) throw new ArgumentOutOfRangeException(nameof(failedAttempt));
        if (baseDelayMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(baseDelayMilliseconds));
        if (maxDelayMilliseconds < baseDelayMilliseconds) throw new ArgumentOutOfRangeException(nameof(maxDelayMilliseconds));
        if (double.IsNaN(jitterSample) || double.IsInfinity(jitterSample) || jitterSample < 0 || jitterSample >= 1)
            throw new ArgumentOutOfRangeException(nameof(jitterSample));
        var ceiling = Math.Min(baseDelayMilliseconds * Math.Pow(2d, failedAttempt - 1), maxDelayMilliseconds);
        return TimeSpan.FromMilliseconds(jitterSample * ceiling);
    }
}