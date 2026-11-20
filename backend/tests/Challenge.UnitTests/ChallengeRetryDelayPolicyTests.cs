using System;
using Challenge.Infrastructure.Messaging;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeRetryDelayPolicyTests
{
    [Theory]
    [InlineData(1, 250, 2000, 0.0, 0.0)]
    [InlineData(1, 250, 2000, 0.5, 125.0)]
    [InlineData(2, 250, 2000, 0.5, 250.0)]
    [InlineData(3, 250, 2000, 0.5, 500.0)]
    [InlineData(4, 250, 1000, 0.5, 500.0)]
    [InlineData(8, 250, 1000, 0.75, 750.0)]
    public void GetDelay_UsesFullJitterWithinBoundedExponentialCeiling(
        int attempt,
        int baseDelay,
        int maxDelay,
        double jitterSample,
        double expectedMilliseconds)
    {
        var delay = ChallengeRetryDelayPolicy.GetDelay(
            attempt, baseDelay, maxDelay, jitterSample);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), delay);
    }

    [Fact]
    public void GetDelay_RejectsInvalidPolicyInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChallengeRetryDelayPolicy.GetDelay(0, 250, 2000, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChallengeRetryDelayPolicy.GetDelay(1, 0, 2000, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChallengeRetryDelayPolicy.GetDelay(1, 250, 249, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChallengeRetryDelayPolicy.GetDelay(1, 250, 2000, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChallengeRetryDelayPolicy.GetDelay(1, 250, 2000, double.NaN));
    }
}
