using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace EventTracking.API.BackgroundServices;
public sealed class EventTrackingOutboxDispatcherOptions
{
    public const string SectionName = "EventTrackingOutbox";
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 10;
    public int BaseRetryDelaySeconds { get; set; } = 1;
    public int MaxRetryDelaySeconds { get; set; } = 300;
    public void Validate()
    {
        if (!Enabled) return;
        if (PollIntervalSeconds is < 1 or > 300 || BatchSize is < 1 or > 10 ||
            LeaseDurationSeconds is < 30 or > 600 || MaxAttempts is < 1 or > 100 ||
            BaseRetryDelaySeconds is < 1 or > 300 || MaxRetryDelaySeconds < BaseRetryDelaySeconds || MaxRetryDelaySeconds > 3600)
            throw new InvalidOperationException("EventTrackingOutbox configuration contains an out-of-range value.");
    }
}
public sealed class EventTrackingOutboxDispatcher(
    IServiceScopeFactory scopeFactory, IEventTrackingPublisher publisher,
    IOptions<EventTrackingOutboxDispatcherOptions> options, TimeProvider timeProvider,
    ILogger<EventTrackingOutboxDispatcher> logger) : BackgroundService
{
    private readonly EventTrackingOutboxDispatcherOptions _options = options.Value;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) { logger.LogInformation("Event Tracking outbox dispatcher is disabled."); return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IEventTrackingOutboxStore>();
                var batch = await store.ClaimBatchAsync(_options.BatchSize, TimeSpan.FromSeconds(_options.LeaseDurationSeconds), stoppingToken);
                if (batch.Count > 0)
                {
                    IReadOnlyList<EventTrackingPublishResult> results;
                    try { results = await publisher.PublishBatchAsync(batch, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        logger.LogError("Event Tracking outbox publisher failed ({ErrorType}).", exception.GetType().Name);
                        results = batch.Select(x => new EventTrackingPublishResult(x.EventId, EventTrackingPublishOutcome.SystemicFailure, "PublisherUnavailable")).ToArray();
                    }
                    var byId = results.GroupBy(x => x.EventId).ToDictionary(x => x.Key, x => x.First());
                    foreach (var message in batch)
                    {
                        if (!byId.TryGetValue(message.EventId, out var result))
                            result = new EventTrackingPublishResult(message.EventId, EventTrackingPublishOutcome.RetryableFailure, "MissingPublishResult");
                        var now = timeProvider.GetUtcNow().UtcDateTime;
                        if (result.Outcome == EventTrackingPublishOutcome.Accepted)
                            await store.MarkPublishedAsync(message.EventId, message.LeaseToken, now, stoppingToken);
                        else if (message.AttemptCount >= _options.MaxAttempts)
                        {
                            await store.QuarantineAsync(message.EventId, message.LeaseToken, now, result.ErrorCode, stoppingToken);
                            logger.LogError("Event Tracking outbox message quarantined. EventId={EventId} ErrorCode={ErrorCode}", message.EventId, result.ErrorCode);
                        }
                        else
                        {
                            var exponent = Math.Min(message.AttemptCount - 1, 20);
                            var ceiling = Math.Min(_options.BaseRetryDelaySeconds * Math.Pow(2, exponent), _options.MaxRetryDelaySeconds);
                            var multiplier = result.Outcome == EventTrackingPublishOutcome.SystemicFailure ? 1d : Random.Shared.NextDouble();
                            await store.ScheduleRetryAsync(message.EventId, message.LeaseToken, now.AddSeconds(Math.Max(1, ceiling * multiplier)), result.ErrorCode, stoppingToken);
                            if (result.Outcome == EventTrackingPublishOutcome.SystemicFailure)
                                delay = TimeSpan.FromSeconds(Math.Min(_options.MaxRetryDelaySeconds, _options.BaseRetryDelaySeconds * 2));
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Event Tracking outbox iteration failed ({ErrorType}).", exception.GetType().Name);
                delay = TimeSpan.FromSeconds(Math.Min(30, _options.MaxRetryDelaySeconds));
            }
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
