using System;
using System.Text;

namespace Identity.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string Uri { get; set; } = "amqp://teampulse:local_rabbitmq_password@localhost:5672/";
    public string ExchangeName { get; set; } = "teampulse.domain.events";
    public string RoutingKey { get; set; } = "identity.employee.lifecycle";

    public void Validate()
    {
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != "amqp" && parsedUri.Scheme != "amqps") ||
            string.IsNullOrWhiteSpace(parsedUri.Host))
            throw new InvalidOperationException("RabbitMq:Uri must be an absolute amqp or amqps URI with a host.");

        if (string.IsNullOrWhiteSpace(ExchangeName) || ExchangeName.Length > 255 || ExchangeName.Contains(' '))
            throw new InvalidOperationException("RabbitMq:ExchangeName must be a non-empty name of at most 255 characters.");

        if (string.IsNullOrWhiteSpace(RoutingKey) || Encoding.UTF8.GetByteCount(RoutingKey) > 255)
            throw new InvalidOperationException("RabbitMq:RoutingKey must be non-empty and no longer than 255 UTF-8 bytes.");
    }
}
