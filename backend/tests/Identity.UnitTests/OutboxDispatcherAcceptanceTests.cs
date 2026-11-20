using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Identity.API.BackgroundServices;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Identity.UnitTests;

public sealed class OutboxDispatcherOptionsAcceptanceTests
{
    [Fact]
    public void DisabledDispatcher_DoesNotRequireAwsConfiguration()
    {
        var options = new OutboxDispatcherOptions();

        Assert.False(options.Enabled);
        options.Validate(string.Empty, string.Empty);
    }

    [Fact]
    public void EnabledDispatcher_AcceptsValidConfiguration()
    {
        var options = new OutboxDispatcherOptions { Enabled = true };

        options.Validate("teampulse-nonprod", "us-east-1");
    }

    [Theory]
    [InlineData(null, "us-east-1")]
    [InlineData("", "us-east-1")]
    [InlineData(" ", "us-east-1")]
    [InlineData("teampulse-nonprod", null)]
    [InlineData("teampulse-nonprod", "")]
    [InlineData("teampulse-nonprod", " ")]
    public void EnabledDispatcher_RequiresBusAndRegion(string? busName, string? region)
    {
        var options = new OutboxDispatcherOptions { Enabled = true };

        Assert.Throws<InvalidOperationException>(() => options.Validate(busName!, region!));
    }

    [Theory]
    [InlineData(0, 10, 60, 10, 1, 300, 30)]
    [InlineData(2, 11, 60, 10, 1, 300, 30)]
    [InlineData(2, 10, 29, 10, 1, 300, 30)]
    [InlineData(2, 10, 60, 0, 1, 300, 30)]
    [InlineData(2, 10, 60, 10, 0, 300, 30)]
    [InlineData(2, 10, 60, 10, 5, 4, 30)]
    [InlineData(2, 10, 60, 10, 1, 3601, 30)]
    [InlineData(2, 10, 60, 10, 1, 300, 4)]
    public void EnabledDispatcher_RejectsOutOfRangeOptions(
        int pollInterval,
        int batchSize,
        int leaseDuration,
        int maxAttempts,
        int baseRetryDelay,
        int maxRetryDelay,
        int systemicPause)
    {
        var options = new OutboxDispatcherOptions
        {
            Enabled = true,
            PollIntervalSeconds = pollInterval,
            BatchSize = batchSize,
            LeaseDurationSeconds = leaseDuration,
            MaxAttempts = maxAttempts,
            BaseRetryDelaySeconds = baseRetryDelay,
            MaxRetryDelaySeconds = maxRetryDelay,
            SystemicFailurePauseSeconds = systemicPause
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate("teampulse-nonprod", "us-east-1"));
    }
}

public sealed class OutboxRetryDelayPolicyTests
{
    [Fact]
    public void CalculateDelay_UsesFullJitterAndCapsExponentialBackoff()
    {
        Assert.Equal(TimeSpan.Zero, OutboxRetryDelayPolicy.CalculateDelay(1, 1, 300, 0));
        Assert.Equal(TimeSpan.FromSeconds(1), OutboxRetryDelayPolicy.CalculateDelay(2, 1, 300, 0.5));
        Assert.True(OutboxRetryDelayPolicy.CalculateDelay(1, 1, 300, 0.999999) < TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(150), OutboxRetryDelayPolicy.CalculateDelay(12, 1, 300, 0.5));
        Assert.Equal(TimeSpan.FromMilliseconds(2500), OutboxRetryDelayPolicy.CalculateDelay(12, 1, 5, 0.5));
    }

    [Fact]
    public void CalculateDelay_RejectsInvalidAttemptConfigurationAndJitter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryDelayPolicy.CalculateDelay(0, 1, 5, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryDelayPolicy.CalculateDelay(1, 0, 5, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryDelayPolicy.CalculateDelay(1, 5, 4, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryDelayPolicy.CalculateDelay(1, 1, 5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetryDelayPolicy.CalculateDelay(1, 1, 5, double.NaN));
    }
}

public sealed class EventBridgeFailureClassificationAcceptanceTests
{
    [Theory]
    [InlineData("AccessDeniedException", OutboxPublishOutcome.SystemicFailure)]
    [InlineData("ResourceNotFoundException", OutboxPublishOutcome.SystemicFailure)]
    [InlineData("ThrottlingException", OutboxPublishOutcome.RetryableFailure)]
    [InlineData("InternalFailure", OutboxPublishOutcome.RetryableFailure)]
    [InlineData("MalformedDetail", OutboxPublishOutcome.PermanentFailure)]
    public async Task PublishBatch_ClassifiesEntryFailures(
        string errorCode,
        OutboxPublishOutcome expectedOutcome)
    {
        var client = new Mock<IAmazonEventBridge>();
        client.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = new List<PutEventsResultEntry>
                {
                    new() { ErrorCode = errorCode, ErrorMessage = "sensitive transport detail" }
                }
            });

        var publisher = new EventBridgePublisher(client.Object, "teampulse-nonprod");
        var result = Assert.Single(await publisher.PublishBatchAsync(
            new[] { CreateMessage() }, CancellationToken.None));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.DoesNotContain("sensitive transport detail", result.ErrorCode);
    }

    [Fact]
    public async Task PublishBatch_InconsistentFailedEntryCountDoesNotAcknowledgeAnyEntry()
    {
        var client = new Mock<IAmazonEventBridge>();
        client.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = new List<PutEventsResultEntry> { new() { EventId = "accepted" } }
            });

        var publisher = new EventBridgePublisher(client.Object, "teampulse-nonprod");
        var result = Assert.Single(await publisher.PublishBatchAsync(
            new[] { CreateMessage() }, CancellationToken.None));

        Assert.Equal(OutboxPublishOutcome.RetryableFailure, result.Outcome);
        Assert.Equal("InconsistentPutEventsResponse", result.ErrorCode);
    }

    [Fact]
    public async Task PublishBatch_MissingEventIdWithoutErrorIsRetryable()
    {
        var client = new Mock<IAmazonEventBridge>();
        client.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = new List<PutEventsResultEntry> { new() }
            });

        var publisher = new EventBridgePublisher(client.Object, "teampulse-nonprod");
        var result = Assert.Single(await publisher.PublishBatchAsync(
            new[] { CreateMessage() }, CancellationToken.None));

        Assert.Equal(OutboxPublishOutcome.RetryableFailure, result.Outcome);
        Assert.Equal("InvalidPutEventsEntryResponse", result.ErrorCode);
    }

    private static OutboxDispatchMessage CreateMessage() => new(
        Guid.NewGuid(), "EmployeeDeactivated", 1, DateTime.UtcNow,
        Guid.NewGuid(), "{\"eventId\":\"stable-event\"}", Guid.NewGuid(), 1);
}

public sealed class OutboxDispatcherLoggingAcceptanceTests
{
    [Fact]
    public async Task WorkerLogsDoNotContainEventPayload()
    {
        const string secretPayload = "private-employee-note-must-never-be-logged";
        var message = new OutboxDispatchMessage(
            Guid.NewGuid(), "EmployeeDeactivated", 1, DateTime.UtcNow,
            Guid.NewGuid(), "{\"eventId\":\"stable\",\"note\":\"" + secretPayload + "\"}",
            Guid.NewGuid(), 1);
        var store = new SingleMessageOutboxStore(message);
        var logger = new CapturingLogger<OutboxDispatcherWorker>();

        var services = new ServiceCollection();
        services.AddSingleton<IOutboxMessageStore>(store);
        using var provider = services.BuildServiceProvider();

        var worker = new OutboxDispatcherWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PermanentFailurePublisher(),
            Options.Create(new OutboxDispatcherOptions
            {
                Enabled = true,
                PollIntervalSeconds = 30,
                BatchSize = 10,
                LeaseDurationSeconds = 60,
                MaxAttempts = 10,
                BaseRetryDelaySeconds = 1,
                MaxRetryDelaySeconds = 300,
                SystemicFailurePauseSeconds = 30
            }),
            TimeProvider.System,
            logger);

        await worker.StartAsync(CancellationToken.None);
        var observed = await Task.WhenAny(store.SnapshotObserved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(store.SnapshotObserved.Task, observed);
        await worker.StopAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, entry => entry.Contains("quarantined", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Messages, entry => entry.Contains(secretPayload, StringComparison.Ordinal));
    }

    private sealed class PermanentFailurePublisher : IEventPublisher
    {
        public Task<IReadOnlyList<OutboxPublishResult>> PublishBatchAsync(
            IReadOnlyList<OutboxDispatchMessage> messages,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxPublishResult>>(messages.Select(message =>
                new OutboxPublishResult(message.EventId, OutboxPublishOutcome.PermanentFailure, "MalformedDetail"))
                .ToArray());
    }

    private sealed class SingleMessageOutboxStore(OutboxDispatchMessage message) : IOutboxMessageStore
    {
        private int _claimCount;
        public TaskCompletionSource SnapshotObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<OutboxDispatchMessage>> ClaimBatchAsync(
            int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _claimCount) == 1)
                return Task.FromResult<IReadOnlyList<OutboxDispatchMessage>>(new[] { message });
            return Task.FromResult<IReadOnlyList<OutboxDispatchMessage>>(Array.Empty<OutboxDispatchMessage>());
        }

        public Task<bool> MarkPublishedAsync(
            Guid eventId, Guid leaseToken, DateTime publishedAtUtc, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> ScheduleRetryAsync(
            Guid eventId, Guid leaseToken, DateTime nextAttemptAtUtc, string errorCode,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> QuarantineAsync(
            Guid eventId, Guid leaseToken, DateTime deadLetteredAtUtc, string errorCode,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> ReleaseForSystemicFailureAsync(
            Guid eventId, Guid leaseToken, DateTime retryAtUtc, string errorCode,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<OutboxQueueSnapshot> GetQueueSnapshotAsync(CancellationToken cancellationToken)
        {
            SnapshotObserved.TrySetResult();
            return Task.FromResult(new OutboxQueueSnapshot(0, null, 0, 0, 1));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> _messages = new();
        public IReadOnlyCollection<string> Messages => _messages.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Enqueue(formatter(state, exception));

        private sealed class NoopScope : IDisposable
        {
            public static NoopScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}

public sealed class OutboxDispatcherReplayIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public OutboxDispatcherReplayIntegrationTests(MySqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RetryScheduling_PersistsDueTimeAndPreventsEarlyReclaim()
    {
        using var apiFactory = _fixture.CreateFactory();
        using var client = apiFactory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);

        var eventId = Guid.NewGuid();
        await using (var scope = apiFactory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.OutboxMessages.Add(new OutboxMessage(
                eventId, "EmployeeDeactivated", 1, DateTime.UtcNow, Guid.NewGuid(),
                "{\\"eventId\\":\\"retry-test\\"}"));
            await database.SaveChangesAsync();
        }

        var store = CreateStore();
        var firstClaim = Assert.Single(await store.ClaimBatchAsync(
            1, TimeSpan.FromSeconds(60), CancellationToken.None));
        var retryAtUtc = DateTime.UtcNow.AddSeconds(2);

        Assert.True(await store.ScheduleRetryAsync(
            firstClaim.EventId, firstClaim.LeaseToken, retryAtUtc,
            "ThrottlingException", CancellationToken.None));
        Assert.Empty(await store.ClaimBatchAsync(1, TimeSpan.FromSeconds(60), CancellationToken.None));

        await Task.Delay(TimeSpan.FromMilliseconds(2200));
        var retryClaim = Assert.Single(await store.ClaimBatchAsync(
            1, TimeSpan.FromSeconds(60), CancellationToken.None));

        Assert.Equal(eventId, retryClaim.EventId);
        Assert.Equal(firstClaim.AttemptCount + 1, retryClaim.AttemptCount);
        Assert.True(await store.MarkPublishedAsync(
            retryClaim.EventId, retryClaim.LeaseToken, DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptedEventWithoutDatabaseAcknowledgement_ReplaysSameEventIdAndEnvelope()
    {
        using var apiFactory = _fixture.CreateFactory();
        using var client = apiFactory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.OK,
            (await client.GetAsync("/health/ready")).StatusCode);

        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var correlationId = Guid.NewGuid();
        var envelope = JsonSerializer.Serialize(new
        {
            eventId,
            eventType = "EmployeeDeactivated",
            schemaVersion = 1,
            occurredAtUtc = occurredAt,
            correlationId,
            payload = new { employeeId = Guid.NewGuid() }
        });

        await using (var scope = apiFactory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.OutboxMessages.Add(new OutboxMessage(
                eventId, "EmployeeDeactivated", 1, occurredAt, correlationId, envelope));
            await database.SaveChangesAsync();
        }

        var store = CreateStore();
        var firstClaim = Assert.Single(await store.ClaimBatchAsync(
            1, TimeSpan.FromMilliseconds(200), CancellationToken.None));

        var requests = new List<PutEventsRequest>();
        var publishCount = 0;
        var eventBridge = new Mock<IAmazonEventBridge>();
        eventBridge.Setup(service => service.PutEventsAsync(
                It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutEventsRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => new PutEventsResponse
            {
                Entries = new List<PutEventsResultEntry>
                {
                    new() { EventId = "accepted-" + Interlocked.Increment(ref publishCount) }
                }
            });

        var publisher = new EventBridgePublisher(eventBridge.Object, "teampulse-nonprod");
        var firstPublish = Assert.Single(await publisher.PublishBatchAsync(
            new[] { firstClaim }, CancellationToken.None));
        Assert.Equal(OutboxPublishOutcome.Accepted, firstPublish.Outcome);

        // Simulate process termination after EventBridge acceptance but before the MySQL acknowledgement.
        await Task.Delay(350);
        var secondClaim = Assert.Single(await store.ClaimBatchAsync(
            1, TimeSpan.FromSeconds(60), CancellationToken.None));

        Assert.Equal(firstClaim.EventId, secondClaim.EventId);
        Assert.Equal(firstClaim.Detail, secondClaim.Detail);
        Assert.Equal(firstClaim.AttemptCount + 1, secondClaim.AttemptCount);

        var secondPublish = Assert.Single(await publisher.PublishBatchAsync(
            new[] { secondClaim }, CancellationToken.None));
        Assert.Equal(OutboxPublishOutcome.Accepted, secondPublish.Outcome);
        Assert.True(await store.MarkPublishedAsync(
            secondClaim.EventId, secondClaim.LeaseToken, DateTime.UtcNow, CancellationToken.None));

        Assert.Equal(2, requests.Count);
        Assert.Equal(envelope, requests[0].Entries.Single().Detail);
        Assert.Equal(envelope, requests[1].Entries.Single().Detail);
        using var parsedEnvelope = JsonDocument.Parse(requests[1].Entries.Single().Detail);
        Assert.Equal(eventId, parsedEnvelope.RootElement.GetProperty("eventId").GetGuid());
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
