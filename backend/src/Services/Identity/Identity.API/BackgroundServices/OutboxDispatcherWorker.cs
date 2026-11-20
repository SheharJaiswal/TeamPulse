using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.API.BackgroundServices;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "OutboxDispatcher";
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 10;
    public int BaseRetryDelaySeconds { get; set; } = 1;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public int SystemicFailurePauseSeconds { get; set; } = 30;

    public void Validate(string? eventBusName, string? awsRegion)
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(eventBusName))
            throw new InvalidOperationException("EventBridge:BusName is required when the outbox dispatcher is enabled.");
        if (string.IsNullOrWhiteSpace(awsRegion))
            throw new InvalidOperationException("AWS_REGION (or AWS:Region) is required when the outbox dispatcher is enabled.");
        if (PollIntervalSeconds is < 1 or > 300 || BatchSize is < 1 or > 10 ||
            LeaseDurationSeconds is < 30 or > 600 || MaxAttempts is < 1 or > 100 ||
            BaseRetryDelaySeconds is < 1 or > 300 ||
            MaxRetryDelaySeconds < BaseRetryDelaySeconds || MaxRetryDelaySeconds > 3600 ||
            SystemicFailurePauseSeconds is < 5 or > 900)
            throw new InvalidOperationException("Outbox dispatcher configuration contains an out-of-range value.");
    }
}

public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<OutboxDispatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private readonly OutboxDispatcherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.Zero;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxMessageStore>();
                var claimed = await store.ClaimBatchAsync(
                    _options.BatchSize, TimeSpan.FromSeconds(_options.LeaseDurationSeconds), stoppingToken);

                if (claimed.Count == 0)
                {
                    delay = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
                }
                else
                {
                    IReadOnlyList<OutboxPublishResult> results;
                    try
                    {
                        results = await publisher.PublishBatchAsync(claimed, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError("Outbox publisher failed. ErrorType={ErrorType}", exception.GetType().Name);
                        results = claimed.Select(message => new OutboxPublishResult(
                            message.EventId, OutboxPublishOutcome.SystemicFailure,
                            "PublisherUnavailable")).ToArray();
                    }

                    var resultById = results.GroupBy(result => result.EventId)
                        .ToDictionary(group => group.Key, group => group.First());
                    var systemicFailure = false;
                    foreach (var message in claimed)
                    {
                        if (!resultById.TryGetValue(message.EventId, out var result))
                            result = new OutboxPublishResult(
                                message.EventId, OutboxPublishOutcome.RetryableFailure, "MissingPublishResult");

                        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                        switch (result.Outcome)
                        {
                            case OutboxPublishOutcome.Accepted:
                                if (await store.MarkPublishedAsync(
                                    message.EventId, message.LeaseToken, nowUtc, stoppingToken))
                                    logger.LogInformation(
                                        "Outbox event accepted. EventId={EventId} EventType={EventType} CorrelationId={CorrelationId} Attempt={AttemptCount}",
                                        message.EventId, message.EventType, message.CorrelationId, message.AttemptCount);
                                else
                                    logger.LogWarning(
                                        "Outbox acknowledgement skipped after lease ownership changed. EventId={EventId}",
                                        message.EventId);
                                break;

                            case OutboxPublishOutcome.PermanentFailure:
                                await store.QuarantineAsync(
                                    message.EventId, message.LeaseToken, nowUtc, result.ErrorCode, stoppingToken);
                                logger.LogError(
                                    "Outbox event quarantined after a permanent event error. EventId={EventId} ErrorCode={ErrorCode}",
                                    message.EventId, result.ErrorCode);
                                break;

                            case OutboxPublishOutcome.SystemicFailure:
                                systemicFailure = true;
                                await store.ReleaseForSystemicFailureAsync(
                                    message.EventId, message.LeaseToken,
                                    nowUtc.AddSeconds(_options.SystemicFailurePauseSeconds),
                                    result.ErrorCode, stoppingToken);
                                logger.LogError(
                                    "Outbox dispatch paused after a systemic publisher failure. ErrorCode={ErrorCode}",
                                    result.ErrorCode);
                                break;

                            default:
                                if (message.AttemptCount >= _options.MaxAttempts)
                                {
                                    await store.QuarantineAsync(
                                        message.EventId, message.LeaseToken, nowUtc, result.ErrorCode, stoppingToken);
                                    logger.LogError(
                                        "Outbox event quarantined after retry limit. EventId={EventId} ErrorCode={ErrorCode}",
                                        message.EventId, result.ErrorCode);
                                }
                                else
                                {
                                    await store.ScheduleRetryAsync(
                                        message.EventId, message.LeaseToken,
                                        nowUtc.Add(NextRetryDelay(message.AttemptCount)),
                                        result.ErrorCode, stoppingToken);
                                    logger.LogWarning(
                                        "Outbox event scheduled for retry. EventId={EventId} Attempt={AttemptCount} ErrorCode={ErrorCode}",
                                        message.EventId, message.AttemptCount, result.ErrorCode);
                                }
                                break;
                        }
                    }

                    if (systemicFailure)
                        delay = TimeSpan.FromSeconds(_options.SystemicFailurePauseSeconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Outbox dispatcher iteration failed. ErrorType={ErrorType}", exception.GetType().Name);
                delay = TimeSpan.FromSeconds(_options.SystemicFailurePauseSeconds);
            }

            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    private TimeSpan NextRetryDelay(int attemptCount)
    {
        var exponent = Math.Min(Math.Max(attemptCount - 1, 0), 20);
        var ceilingSeconds = Math.Min(
            _options.BaseRetryDelaySeconds * Math.Pow(2, exponent),
            _options.MaxRetryDelaySeconds);
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * ceilingSeconds * 1000);
    }
}
