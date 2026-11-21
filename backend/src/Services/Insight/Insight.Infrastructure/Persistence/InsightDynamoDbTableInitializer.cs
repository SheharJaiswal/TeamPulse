using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Insight.Infrastructure.Persistence;

public sealed class InsightDynamoDbTableInitializer
{
    private readonly IAmazonDynamoDB _client;
    private readonly InsightDynamoDbOptions _options;

    public InsightDynamoDbTableInitializer(IAmazonDynamoDB client, InsightDynamoDbOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.CreateTableAsync(new CreateTableRequest
            {
                TableName = _options.TableName,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new("PK", ScalarAttributeType.S),
                    new("SK", ScalarAttributeType.S)
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new("PK", KeyType.HASH),
                    new("SK", KeyType.RANGE)
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            }, cancellationToken);
        }
        catch (ResourceInUseException)
        {
            // The configured local table already exists; verify it becomes ACTIVE below.
        }

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var response = await _client.DescribeTableAsync(_options.TableName, cancellationToken);
            if (response.Table.TableStatus == TableStatus.ACTIVE)
                return;
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException($"DynamoDB table '{_options.TableName}' did not become ACTIVE.");
    }
}