using System;

namespace Notification.Infrastructure.Messaging;

public sealed class NotificationRabbitMqConsumerOptions
{
    public const string SectionName = "NotificationConsumer";
    public bool Enabled { get; set; }
    public string Uri { get; set; } = string.Empty;
    public string QueueName { get; set; } = "teampulse.notification.events";
    public ushort PrefetchCount { get; set; } = 1;
    public int MaxProcessingAttempts { get; set; } = 3;
    public int RetryBaseDelayMilliseconds { get; set; } = 250;
    public int RetryMaxDelayMilliseconds { get; set; } = 2000;
    public int ReconnectDelayMilliseconds { get; set; } = 5000;
    public int ConnectionTimeoutMilliseconds { get; set; } = 5000;
    public int HeartbeatSeconds { get; set; } = 5;

    public void Validate()
    {
        if (!Enabled) return;
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var brokerUri) ||
            (brokerUri.Scheme != "amqp" && brokerUri.Scheme != "amqps") || string.IsNullOrWhiteSpace(brokerUri.Host))
            throw new InvalidOperationException("NotificationConsumer:Uri must be an absolute amqp or amqps URI with a host.");
        if (string.IsNullOrWhiteSpace(QueueName) || QueueName.Length > 255 || QueueName.Contains(' '))
            throw new InvalidOperationException("NotificationConsumer:QueueName must be a non-empty queue name of at most 255 characters.");
        if (PrefetchCount != 1)
            throw new InvalidOperationException("NotificationConsumer:PrefetchCount must be 1.");
        if (MaxProcessingAttempts is < 1 or > 10)
            throw new InvalidOperationException("NotificationConsumer:MaxProcessingAttempts must be between 1 and 10.");
        if (RetryBaseDelayMilliseconds is < 1 or > 30000 ||
            RetryMaxDelayMilliseconds < RetryBaseDelayMilliseconds || RetryMaxDelayMilliseconds > 60000)
            throw new InvalidOperationException("NotificationConsumer retry delay bounds are invalid.");
        if (ReconnectDelayMilliseconds is < 250 or > 60000)
            throw new InvalidOperationException("NotificationConsumer:ReconnectDelayMilliseconds must be between 250 and 60000.");
        if (ConnectionTimeoutMilliseconds is < 1000 or > 30000)
            throw new InvalidOperationException("NotificationConsumer:ConnectionTimeoutMilliseconds must be between 1000 and 30000.");
        if (HeartbeatSeconds is < 1 or > 60)
            throw new InvalidOperationException("NotificationConsumer:HeartbeatSeconds must be between 1 and 60.");
    }
}