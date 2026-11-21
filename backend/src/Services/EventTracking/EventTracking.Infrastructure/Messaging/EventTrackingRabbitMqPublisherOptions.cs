using System;
using System.Text;
namespace EventTracking.Infrastructure.Messaging;
public sealed class EventTrackingRabbitMqPublisherOptions
{
    public const string SectionName = "EventTrackingPublisher";
    public string Uri { get; set; } = "amqp://teampulse:local_rabbitmq_password@localhost:5672/";
    public string ExchangeName { get; set; } = "teampulse.domain.events";
    public string RoutingKey { get; set; } = "habit.activity.recorded";
    public void Validate()
    {
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != "amqp" && parsed.Scheme != "amqps") || string.IsNullOrWhiteSpace(parsed.Host))
            throw new InvalidOperationException("EventTrackingPublisher:Uri must be an absolute amqp or amqps URI with a host.");
        if (string.IsNullOrWhiteSpace(ExchangeName) || ExchangeName.Length > 255 || ExchangeName.Contains(' '))
            throw new InvalidOperationException("EventTrackingPublisher:ExchangeName is invalid.");
        if (string.IsNullOrWhiteSpace(RoutingKey) || Encoding.UTF8.GetByteCount(RoutingKey) > 255)
            throw new InvalidOperationException("EventTrackingPublisher:RoutingKey is invalid.");
    }
}
