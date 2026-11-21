using System;
using EventTracking.Infrastructure.Messaging;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class EventTrackingRabbitMqOptionsTests
{
    [Fact]
    public void RetryDelayPolicy_UsesBoundedFullJitter()
    {
        var delay = EventTrackingRetryDelayPolicy.GetDelay(3, 250, 1000, 0.5);
        Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
    }

    [Fact]
    public void RetryDelayPolicy_RejectsJitterOutsideZeroToOneExclusive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventTrackingRetryDelayPolicy.GetDelay(1, 250, 1000, 1));
    }

    [Fact]
    public void ConsumerOptions_DefaultDisabled_DoesNotRequireBrokerUri()
    {
        var options = new EventTrackingRabbitMqConsumerOptions();
        options.Validate();
        Assert.False(options.Enabled);
    }

    [Fact]
    public void ConsumerOptions_Enabled_RequiresAbsoluteAmqpUri()
    {
        var options = new EventTrackingRabbitMqConsumerOptions { Enabled = true, Uri = "localhost:5672" };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ConsumerOptions_Enabled_RejectsPrefetchAboveOne()
    {
        var options = new EventTrackingRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = "amqp://guest:guest@localhost:5672/",
            PrefetchCount = 2
        };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
