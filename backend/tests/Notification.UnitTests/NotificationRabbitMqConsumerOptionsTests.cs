using System;
using Notification.Infrastructure.Messaging;
using Xunit;
namespace Notification.UnitTests;
public sealed class NotificationRabbitMqConsumerOptionsTests
{
    [Fact]
    public void Validate_DisabledConsumer_DoesNotRequireBrokerUri()
    {
        var options = new NotificationRabbitMqConsumerOptions();
        options.Validate();
    }

    [Fact]
    public void Validate_EnabledConsumer_AcceptsConfiguredAmqpUri()
    {
        var options = new NotificationRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = "amqp://teampulse:password@localhost:5672/"
        };
        options.Validate();
    }

    [Theory]
    [InlineData("http://localhost:5672/")]
    [InlineData("not-a-uri")]
    [InlineData("")]
    public void Validate_EnabledConsumer_RejectsInvalidBrokerUri(string uri)
    {
        var options = new NotificationRabbitMqConsumerOptions { Enabled = true, Uri = uri };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_EnabledConsumer_RejectsPrefetchAboveOne()
    {
        var options = new NotificationRabbitMqConsumerOptions
        {
            Enabled = true,
            Uri = "amqp://localhost",
            PrefetchCount = 2
        };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void RetryDelayPolicy_UsesBoundedExponentialBackoffAndJitter()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(50), NotificationRetryDelayPolicy.GetDelay(1, 100, 250, 0.5));
        Assert.Equal(TimeSpan.FromMilliseconds(100), NotificationRetryDelayPolicy.GetDelay(2, 100, 250, 0.5));
        Assert.Equal(TimeSpan.FromMilliseconds(125), NotificationRetryDelayPolicy.GetDelay(5, 100, 250, 0.5));
    }

    [Theory]
    [InlineData(0, 100, 200, 0.5)]
    [InlineData(1, 0, 200, 0.5)]
    [InlineData(1, 200, 100, 0.5)]
    [InlineData(1, 100, 200, 1.0)]
    public void RetryDelayPolicy_RejectsInvalidInputs(int attempt, int baseDelay, int maxDelay, double jitter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NotificationRetryDelayPolicy.GetDelay(attempt, baseDelay, maxDelay, jitter));
    }
}
