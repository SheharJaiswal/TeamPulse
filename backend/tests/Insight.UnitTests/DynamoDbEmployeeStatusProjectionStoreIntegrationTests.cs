using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Insight.Application.Contracts;
using Insight.Infrastructure.Persistence;
using Xunit;

namespace Insight.UnitTests;

public sealed class DynamoDbEmployeeStatusProjectionStoreIntegrationTests(DynamoDbLocalFixture fixture)
    : IClassFixture<DynamoDbLocalFixture>
{
    [Fact]
    public async Task ProcessAsync_AtomicallyStoresMinimalProjectionAndInbox_ThenDeduplicates()
    {
        using var client = fixture.CreateClient();
        var (store, options) = await CreateStoreAsync(client);
        var employeeId = Guid.NewGuid();
        var lifecycleEvent = CreateEvent(Guid.NewGuid(), employeeId, 1, false);

        Assert.Equal(ProjectionProcessingOutcome.Applied,
            await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Duplicate,
            await store.ProcessAsync(lifecycleEvent, DateTime.UtcNow, CancellationToken.None));

        var projection = await client.GetItemAsync(new GetItemRequest
        {
            TableName = options.TableName,
            Key = ProjectionKey(employeeId),
            ConsistentRead = true
        });
        Assert.Equal(new[] { "PK", "SK", "employeeId", "isActive", "lifecycleVersion", "updatedAtUtc" },
            projection.Item.Keys.OrderBy(x => x).ToArray());
        Assert.Equal($"EMPLOYEE#{employeeId.ToString("D").ToLowerInvariant()}", projection.Item["PK"].S);
        Assert.False(projection.Item["isActive"].BOOL);
        Assert.Equal("1", projection.Item["lifecycleVersion"].N);

        var inbox = await client.GetItemAsync(new GetItemRequest
        {
            TableName = options.TableName,
            Key = InboxKey(lifecycleEvent.EventId),
            ConsistentRead = true
        });
        Assert.Equal(new[] { "PK", "SK", "employeeId", "eventId", "eventType", "lifecycleVersion", "processedAtUtc", "schemaVersion" },
            inbox.Item.Keys.OrderBy(x => x).ToArray());
        Assert.Equal(1, inbox.Item.Count);
    }

    [Fact]
    public async Task ProcessAsync_RecordsStaleAndEqualVersionsWithoutProjectionRegression()
    {
        using var client = fixture.CreateClient();
        var (store, options) = await CreateStoreAsync(client);
        var employeeId = Guid.NewGuid();

        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, false), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 3, true), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Stale, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 4, true), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(ProjectionProcessingOutcome.Applied, await store.ProcessAsync(CreateEvent(Guid.NewGuid(), employeeId, 5, true), DateTime.UtcNow, CancellationToken.None));

        var item = await client.GetItemAsync(new GetItemRequest { TableName = options.TableName, Key = ProjectionKey(employeeId), ConsistentRead = true });
        Assert.Equal("5", item.Item["lifecycleVersion"].N);
        Assert.True(item.Item["isActive"].BOOL);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentIndependentStoresPreserveMaximumLifecycleVersion()
    {
        using var clientA = fixture.CreateClient();
        using var clientB = fixture.CreateClient();
        var (storeA, options) = await CreateStoreAsync(clientA);
        var storeB = new DynamoDbEmployeeStatusProjectionStore(clientB, options);
        var employeeId = Guid.NewGuid();

        var deliveries = Enumerable.Range(1, 12).Select(version =>
            (version % 2 == 0 ? storeA : storeB).ProcessAsync(
                CreateEvent(Guid.NewGuid(), employeeId, version, version % 2 == 0),
                DateTime.UtcNow,
                CancellationToken.None));

        var outcomes = await Task.WhenAll(deliveries);
        Assert.DoesNotContain(ProjectionProcessingOutcome.Duplicate, outcomes);
        Assert.All(outcomes, outcome => Assert.Contains(outcome, new[] { ProjectionProcessingOutcome.Applied, ProjectionProcessingOutcome.Stale }));

        var item = await clientA.GetItemAsync(new GetItemRequest { TableName = options.TableName, Key = ProjectionKey(employeeId), ConsistentRead = true });
        Assert.Equal("12", item.Item["lifecycleVersion"].N);
        Assert.True(item.Item["isActive"].BOOL);
    }

    private static async Task<(DynamoDbEmployeeStatusProjectionStore Store, InsightDynamoDbOptions Options)> CreateStoreAsync(IAmazonDynamoDB client)
    {
        var options = new InsightDynamoDbOptions { TableName = $"insight-test-{Guid.NewGuid():N}" };
        await new InsightDynamoDbTableInitializer(client, options).InitializeAsync(CancellationToken.None);
        return (new DynamoDbEmployeeStatusProjectionStore(client, options), options);
    }

    private static EmployeeLifecycleEvent CreateEvent(Guid eventId, Guid employeeId, long version, bool isActive) =>
        new(eventId, isActive ? "EmployeeReactivated" : "EmployeeDeactivated", 1, employeeId, isActive, version, Guid.NewGuid(), DateTime.UtcNow);

    private static Dictionary<string, AttributeValue> ProjectionKey(Guid employeeId) => new()
    {
        ["PK"] = new() { S = $"EMPLOYEE#{employeeId:D}".ToLowerInvariant() },
        ["SK"] = new() { S = "STATUS" }
    };

    private static Dictionary<string, AttributeValue> InboxKey(Guid eventId) => new()
    {
        ["PK"] = new() { S = $"EVENT#{eventId.ToString("D").ToLowerInvariant()}" },
        ["SK"] = new() { S = "INBOX" }
    };
}