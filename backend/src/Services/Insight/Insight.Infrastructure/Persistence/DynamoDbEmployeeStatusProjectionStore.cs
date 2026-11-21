using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Insight.Application.Abstractions;
using Insight.Application.Contracts;

namespace Insight.Infrastructure.Persistence;

public sealed class DynamoDbEmployeeStatusProjectionStore : IEmployeeStatusProjectionStore
{
    private const int MaxConcurrencyAttempts = 5;
    private readonly IAmazonDynamoDB _client;
    private readonly InsightDynamoDbOptions _options;

    public DynamoDbEmployeeStatusProjectionStore(IAmazonDynamoDB client, InsightDynamoDbOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task<ProjectionProcessingOutcome> ProcessAsync(
        EmployeeLifecycleEvent lifecycleEvent, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        if (processedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Processed timestamp must be UTC.", nameof(processedAtUtc));

        var employeeKey = ProjectionKey(lifecycleEvent.EmployeeId);
        var inboxKey = InboxKey(lifecycleEvent.EventId);

        for (var attempt = 0; attempt < MaxConcurrencyAttempts; attempt++)
        {
            var projectionTask = _client.GetItemAsync(new GetItemRequest
            {
                TableName = _options.TableName,
                Key = employeeKey,
                ConsistentRead = true
            }, cancellationToken);
            var inboxTask = _client.GetItemAsync(new GetItemRequest
            {
                TableName = _options.TableName,
                Key = inboxKey,
                ConsistentRead = true
            }, cancellationToken);
            await Task.WhenAll(projectionTask, inboxTask);

            if (inboxTask.Result.Item is { Count: > 0 })
                return ProjectionProcessingOutcome.Duplicate;

            var current = projectionTask.Result.Item;
            var hasProjection = current is { Count: > 0 };
            var currentVersion = hasProjection
                ? long.Parse(current!["lifecycleVersion"].N, CultureInfo.InvariantCulture)
                : 0L;
            var isNewer = !hasProjection || lifecycleEvent.LifecycleVersion > currentVersion;

            var actions = new List<TransactWriteItem>
            {
                new()
                {
                    Put = new Put
                    {
                        TableName = _options.TableName,
                        Item = InboxItem(lifecycleEvent, processedAtUtc),
                        ConditionExpression = "attribute_not_exists(#pk)",
                        ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = "PK" }
                    }
                }
            };

            if (isNewer)
            {
                var condition = hasProjection ? "#version = :observed" : "attribute_not_exists(#pk)";
                var values = new Dictionary<string, AttributeValue>
                {
                    [":employeeId"] = new() { S = lifecycleEvent.EmployeeId.ToString("D").ToLowerInvariant() },
                    [":isActive"] = new() { BOOL = lifecycleEvent.IsActive },
                    [":version"] = new() { N = lifecycleEvent.LifecycleVersion.ToString(CultureInfo.InvariantCulture) },
                    [":updatedAtUtc"] = new() { S = processedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
                };
                if (hasProjection)
                    values[":observed"] = new AttributeValue { N = currentVersion.ToString(CultureInfo.InvariantCulture) };

                actions.Add(new TransactWriteItem
                {
                    Update = new Update
                    {
                        TableName = _options.TableName,
                        Key = employeeKey,
                        UpdateExpression = "SET #employeeId = :employeeId, #isActive = :isActive, #version = :version, #updatedAtUtc = :updatedAtUtc",
                        ConditionExpression = condition,
                        ExpressionAttributeNames = ProjectionNames(),
                        ExpressionAttributeValues = values
                    }
                });
            }
            else
            {
                actions.Add(new TransactWriteItem
                {
                    ConditionCheck = new ConditionCheck
                    {
                        TableName = _options.TableName,
                        Key = employeeKey,
                        ConditionExpression = "#version >= :incoming",
                        ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "lifecycleVersion" },
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                        {
                            [":incoming"] = new() { N = lifecycleEvent.LifecycleVersion.ToString(CultureInfo.InvariantCulture) }
                        }
                    }
                });
            }

            try
            {
                await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = actions }, cancellationToken);
                return isNewer ? ProjectionProcessingOutcome.Applied : ProjectionProcessingOutcome.Stale;
            }
            catch (TransactionCanceledException)
            {
                // A cancellation can be an inbox race OR a projection-version race. Only
                // the strongly-read inbox item is sufficient evidence of a duplicate.
                var inboxAfterCancellation = await _client.GetItemAsync(new GetItemRequest
                {
                    TableName = _options.TableName,
                    Key = inboxKey,
                    ConsistentRead = true
                }, cancellationToken);
                if (inboxAfterCancellation.Item is { Count: > 0 })
                    return ProjectionProcessingOutcome.Duplicate;

                if (attempt == MaxConcurrencyAttempts - 1)
                    throw;

                await Task.Delay(TimeSpan.FromMilliseconds(15 * (attempt + 1)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Insight projection concurrency retry budget was exhausted.");
    }

    private Dictionary<string, AttributeValue> InboxItem(EmployeeLifecycleEvent item, DateTime processedAtUtc) =>
        new()
        {
            ["PK"] = new() { S = $"EVENT#{item.EventId.ToString("D").ToLowerInvariant()}" },
            ["SK"] = new() { S = "INBOX" },
            ["eventId"] = new() { S = item.EventId.ToString("D").ToLowerInvariant() },
            ["eventType"] = new() { S = item.EventType },
            ["schemaVersion"] = new() { N = item.SchemaVersion.ToString(CultureInfo.InvariantCulture) },
            ["employeeId"] = new() { S = item.EmployeeId.ToString("D").ToLowerInvariant() },
            ["lifecycleVersion"] = new() { N = item.LifecycleVersion.ToString(CultureInfo.InvariantCulture) },
            ["processedAtUtc"] = new() { S = processedAtUtc.ToString("O", CultureInfo.InvariantCulture) }
        };

    private static Dictionary<string, AttributeValue> ProjectionKey(Guid employeeId) => new()
    {
        ["PK"] = new() { S = $"EMPLOYEE#{employeeId.ToString("D").ToLowerInvariant()}" },
        ["SK"] = new() { S = "STATUS" }
    };

    private static Dictionary<string, AttributeValue> InboxKey(Guid eventId) => new()
    {
        ["PK"] = new() { S = $"EVENT#{eventId.ToString("D").ToLowerInvariant()}" },
        ["SK"] = new() { S = "INBOX" }
    };

    private static Dictionary<string, string> ProjectionNames() => new()
    {
        ["#pk"] = "PK", ["#sk"] = "SK", ["#employeeId"] = "employeeId",
        ["#isActive"] = "isActive", ["#version"] = "lifecycleVersion", ["#updatedAtUtc"] = "updatedAtUtc"
    };
}