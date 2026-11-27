namespace Insight.Infrastructure.Messaging;

public sealed class InsightActivityConsumerOptions
{
    public const string SectionName = "InsightActivityConsumer";
    public bool Enabled { get; set; }
    public string Uri { get; set; } = string.Empty;
    public string QueueName { get; set; } = "teampulse.insight.activity.events";
    public ushort PrefetchCount { get; set; } = 1;
    public int MaxProcessingAttempts { get; set; } = 3;

    public void Validate()
    {
        if (!Enabled) return;
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var brokerUri) ||
            (brokerUri.Scheme != "amqp" && brokerUri.Scheme != "amqps") ||
            string.IsNullOrWhiteSpace(brokerUri.Host))
            throw new InvalidOperationException("InsightActivityConsumer:Uri must be an absolute amqp or amqps URI.");
        if (string.IsNullOrWhiteSpace(QueueName) || QueueName.Length > 255 || QueueName.Contains(' '))
            throw new InvalidOperationException("InsightActivityConsumer:QueueName is invalid.");
        if (PrefetchCount != 1)
            throw new InvalidOperationException("InsightActivityConsumer:PrefetchCount must be 1.");
        if (MaxProcessingAttempts is < 1 or > 10)
            throw new InvalidOperationException("InsightActivityConsumer:MaxProcessingAttempts must be between 1 and 10.");
    }
}
