using Amazon.DynamoDBv2;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Insight.Infrastructure.Persistence;

public sealed class InsightDynamoDbHealthCheck : IHealthCheck
{
    private readonly IAmazonDynamoDB _client;
    private readonly InsightDynamoDbOptions _options;

    public InsightDynamoDbHealthCheck(IAmazonDynamoDB client, InsightDynamoDbOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.DescribeTableAsync(_options.TableName, cancellationToken);
            return response.Table.TableStatus == TableStatus.ACTIVE
                ? HealthCheckResult.Healthy("Insight DynamoDB table is ACTIVE.")
                : HealthCheckResult.Unhealthy("Insight DynamoDB table is not ACTIVE.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Insight DynamoDB table check failed.", exception);
        }
    }
}