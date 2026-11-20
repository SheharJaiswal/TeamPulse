using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Identity.UnitTests;

public sealed class OutboxMessageDomainTests
{
    [Fact]
    public void Lease_CannotBeAcquiredTwiceWhileCurrentLeaseIsActive()
    {
        var now = DateTime.UtcNow;
        var message = CreateMessage();
        Assert.True(message.TryAcquireLease(Guid.NewGuid(), now, now.AddSeconds(60)));
        Assert.False(message.TryAcquireLease(Guid.NewGuid(), now.AddSeconds(1), now.AddSeconds(61)));
        Assert.Equal(1, message.AttemptCount);
    }

    [Fact]
    public void ExpiredLease_CanBeReclaimed_AndOldTokenCannotAcknowledge()
    {
        var now = DateTime.UtcNow;
        var message = CreateMessage();
        var oldToken = Guid.NewGuid();
        var newToken = Guid.NewGuid();

        Assert.True(message.TryAcquireLease(oldToken, now, now.AddSeconds(1)));
        Assert.True(message.TryAcquireLease(newToken, now.AddSeconds(2), now.AddSeconds(62)));
        Assert.False(message.TryMarkPublished(oldToken, now.AddSeconds(3)));
        Assert.True(message.TryMarkPublished(newToken, now.AddSeconds(3)));
        Assert.NotNull(message.PublishedAtUtc);
        Assert.Null(message.LeaseToken);
    }

    [Fact]
    public void RetryAndQuarantine_ClearLeaseAndPreserveStableEventIdentity()
    {
        var now = DateTime.UtcNow;
        var message = CreateMessage();
        var eventId = message.EventId;
        var token = Guid.NewGuid();

        Assert.True(message.TryAcquireLease(token, now, now.AddSeconds(60)));
        Assert.True(message.TryScheduleRetry(token, now.AddMinutes(1), "ThrottlingException"));
        Assert.Null(message.LeaseToken);
        Assert.Equal("ThrottlingException", message.LastErrorCode);

        var nextToken = Guid.NewGuid();
        Assert.True(message.TryAcquireLease(nextToken, now.AddMinutes(2), now.AddMinutes(3)));
        Assert.True(message.TryQuarantine(nextToken, now.AddMinutes(2), "MalformedDetail"));
        Assert.Equal(eventId, message.EventId);
        Assert.NotNull(message.DeadLetteredAtUtc);
        Assert.Null(message.LeaseToken);
        Assert.False(message.TryAcquireLease(Guid.NewGuid(), now.AddHours(1), now.AddHours(2)));
    }

    private static OutboxMessage CreateMessage() => new(
        Guid.NewGuid(), "EmployeeDeactivated", 1, DateTime.UtcNow,
        Guid.NewGuid(), "{\"eventId\":\"stable\"}");
}

public sealed class EventBridgePublisherTests
{
    [Fact]
    public async Task PublishBatch_PreservesFullEnvelopeAndMapsRoutingMetadata()
    {
        var client = new Mock<IAmazonEventBridge>();
        PutEventsRequest? captured = null;
        client.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutEventsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PutEventsResponse
            {
                Entries = new List<PutEventsResultEntry> { new() { EventId = "accepted" } }
            });

        var occurredAt = DateTime.UtcNow;
        const string envelope = "{\"eventId\":\"stable-event-id\",\"eventType\":\"EmployeeDeactivated\",\"schemaVersion\":1}";
        var message = new OutboxDispatchMessage(
            Guid.NewGuid(), "EmployeeDeactivated", 1, occurredAt,
            Guid.NewGuid(), envelope, Guid.NewGuid(), 1);
        var publisher = new EventBridgePublisher(client.Object, "teampulse-nonprod");

        var result = await publisher.PublishBatchAsync(new[] { message }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(OutboxPublishOutcome.Accepted, result[0].Outcome);
        var entry = Assert.Single(captured!.Entries);
        Assert.Equal("teampulse-nonprod", entry.EventBusName);
        Assert.Equal("teampulse.identity", entry.Source);
        Assert.Equal("EmployeeDeactivated", entry.DetailType);
        Assert.Equal(occurredAt, entry.Time);
        Assert.Equal(envelope, entry.Detail);
    }

    [Fact]
    public async Task PublishBatch_MapsPartialFailurePerEntry()
    {
        var client = new Mock<IAmazonEventBridge>();
        client.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = new List<PutEventsResultEntry>
                {
                    new() { EventId = "accepted" },
                    new() { ErrorCode = "ThrottlingException", ErrorMessage = "do not persist" }
                }
            });

        var publisher = new EventBridgePublisher(client.Object, "teampulse-nonprod");
        var results = await publisher.PublishBatchAsync(
            new[] { CreateMessage(), CreateMessage() }, CancellationToken.None);

        Assert.Equal(OutboxPublishOutcome.Accepted, results[0].Outcome);
        Assert.Equal(OutboxPublishOutcome.RetryableFailure, results[1].Outcome);
        Assert.Equal("ThrottlingException", results[1].ErrorCode);
        Assert.DoesNotContain(results, result => result.ErrorCode.Contains("do not persist"));
    }

    private static OutboxDispatchMessage CreateMessage() => new(
        Guid.NewGuid(), "EmployeeReactivated", 1, DateTime.UtcNow,
        Guid.NewGuid(), "{\"eventId\":\"event\"}", Guid.NewGuid(), 1);
}

public sealed class OutboxMessageStoreIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public OutboxMessageStoreIntegrationTests(MySqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ConcurrentWorkers_ClaimDisjointOutboxRows()
    {
        using var apiFactory = _fixture.CreateFactory();
        using var client = apiFactory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);

        var eventIds = await SeedMessagesAsync(apiFactory, 2);
        var storeA = CreateStore();
        var storeB = CreateStore();
        var claims = await Task.WhenAll(
            storeA.ClaimBatchAsync(1, TimeSpan.FromSeconds(60), CancellationToken.None),
            storeB.ClaimBatchAsync(1, TimeSpan.FromSeconds(60), CancellationToken.None));

        Assert.Single(claims[0]);
        Assert.Single(claims[1]);
        Assert.NotEqual(claims[0][0].EventId, claims[1][0].EventId);
        Assert.Contains(claims[0][0].EventId, eventIds);
        Assert.Contains(claims[1][0].EventId, eventIds);
    }

    [Fact]
    public async Task ExpiredLease_CanBeReclaimed_AndStaleOwnerCannotAcknowledge()
    {
        using var apiFactory = _fixture.CreateFactory();
        using var client = apiFactory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);

        var eventId = (await SeedMessagesAsync(apiFactory, 1)).Single();
        var store = CreateStore();
        var firstClaim = (await store.ClaimBatchAsync(
            1, TimeSpan.FromMilliseconds(100), CancellationToken.None)).Single();
        await Task.Delay(150);
        var secondClaim = (await store.ClaimBatchAsync(
            1, TimeSpan.FromSeconds(60), CancellationToken.None)).Single();

        Assert.Equal(eventId, firstClaim.EventId);
        Assert.Equal(eventId, secondClaim.EventId);
        Assert.NotEqual(firstClaim.LeaseToken, secondClaim.LeaseToken);
        Assert.False(await store.MarkPublishedAsync(
            eventId, firstClaim.LeaseToken, DateTime.UtcNow, CancellationToken.None));
        Assert.True(await store.MarkPublishedAsync(
            eventId, secondClaim.LeaseToken, DateTime.UtcNow, CancellationToken.None));
    }

    private async Task<List<Guid>> SeedMessagesAsync(IdentityApiFactory apiFactory, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        await using var scope = apiFactory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        foreach (var id in ids)
        {
            database.OutboxMessages.Add(new OutboxMessage(
                id, "EmployeeDeactivated", 1, DateTime.UtcNow, Guid.NewGuid(),
                "{\"eventId\":\"" + id + "\",\"eventType\":\"EmployeeDeactivated\"}"));
        }
        await database.SaveChangesAsync();
        return ids;
    }

    private OutboxMessageStore CreateStore()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySql(_fixture.ConnectionString, ServerVersion.AutoDetect(_fixture.ConnectionString))
            .Options;
        IDbContextFactory<IdentityDbContext> factory =
            new PooledDbContextFactory<IdentityDbContext>(options);
        return new OutboxMessageStore(factory, TimeProvider.System);
    }
}
