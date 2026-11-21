namespace Insight.Infrastructure.Persistence;

public sealed class InsightDynamoDbOptions
{
    public const string SectionName = "InsightDynamoDb";
    public string TableName { get; set; } = "teampulse-insight-lifecycle";
    public string Region { get; set; } = "us-east-1";
    public string? ServiceUrl { get; set; }
    public bool InitializeLocalTable { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TableName))
            throw new InvalidOperationException("Insight DynamoDB table name is required.");
        if (string.IsNullOrWhiteSpace(Region))
            throw new InvalidOperationException("Insight DynamoDB region is required.");
        if (InitializeLocalTable && string.IsNullOrWhiteSpace(ServiceUrl))
            throw new InvalidOperationException("Local table initialization requires a DynamoDB Local endpoint.");
    }
}