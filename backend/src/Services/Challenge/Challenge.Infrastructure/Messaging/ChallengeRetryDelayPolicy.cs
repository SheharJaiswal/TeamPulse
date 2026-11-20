using System;

namespace Challenge.Infrastructure.Messaging;

/// <summary>
/// Calculates bounded exponential full-jitter retry delays.
/// The caller supplies the random sample so the policy can be tested deterministically.
/// </summary>
public static class ChallengeRetryDelayPolicy
{
    public static TimeSpan GetDelay(
        int failedAttempt,
        int baseDelayMilliseconds,
        int maxDelayMilliseconds,
        double jitterSample)
    {
        if (failedAttempt < 1)
            throw new ArgumentOutOfRangeException(nameof(failedAttempt));
        if (baseDelayMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(baseDelayMilliseconds));
        if (maxDelayMilliseconds < baseDelayMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(maxDelayMilliseconds));
        if (double.IsNaN(jitterSample) || double.IsInfinity(jitterSample) ||
            jitterSample < 0 || jitterSample >= 1)
            throw new ArgumentOutOfRangeException(nameof(jitterSample));

        var exponentialCeiling = baseDelayMilliseconds * Math.Pow(2d, failedAttempt - 1);
        var boundedCeiling = Math.Min(exponentialCeiling, maxDelayMilliseconds);
        return TimeSpan.FromMilliseconds(jitterSample * boundedCeiling);
    }
}
