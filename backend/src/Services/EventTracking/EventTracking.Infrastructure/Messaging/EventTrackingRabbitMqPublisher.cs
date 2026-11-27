using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using RabbitMQ.Client;
namespace EventTracking.Infrastructure.Messaging;
public sealed class EventTrackingRabbitMqPublisher : IEventTrackingPublisher, IAsyncDisposable
{
    private readonly EventTrackingRabbitMqPublisherOptions _options;
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    public EventTrackingRabbitMqPublisher(EventTrackingRabbitMqPublisherOptions options)
    {
        options.Validate(); _options = options;
        _factory = new ConnectionFactory { Uri = new Uri(options.Uri, UriKind.Absolute),
            ClientProvidedName = "TeamPulse.EventTracking.OutboxPublisher",
            AutomaticRecoveryEnabled = true, TopologyRecoveryEnabled = true, NetworkRecoveryInterval = TimeSpan.FromSeconds(5) };
    }
    public async Task<IReadOnlyList<EventTrackingPublishResult>> PublishBatchAsync(IReadOnlyList<EventTrackingOutboxDispatchMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(messages));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IChannel channel;
            try { channel = await GetChannelAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { return messages.Select(x => new EventTrackingPublishResult(x.EventId, EventTrackingPublishOutcome.SystemicFailure, "RabbitMqUnavailable")).ToArray(); }
            var results = new List<EventTrackingPublishResult>(messages.Count);
            for (var index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                try
                {
                    var properties = new BasicProperties { MessageId = message.EventId.ToString("D"), Type = message.EventType,
                        CorrelationId = message.CorrelationId.ToString("D"), ContentType = "application/json",
                        DeliveryMode = DeliveryModes.Persistent,
                        Headers = new Dictionary<string, object?> { ["schemaVersion"] = message.SchemaVersion } };
                    var routingKey = message.EventType == "ChallengeActivityRecorded" ? "challenge.activity.recorded" : _options.RoutingKey;
                    await channel.BasicPublishAsync(_options.ExchangeName, routingKey, mandatory: true,
                        basicProperties: properties, body: Encoding.UTF8.GetBytes(message.Payload), cancellationToken: cancellationToken);
                    results.Add(new EventTrackingPublishResult(message.EventId, EventTrackingPublishOutcome.Accepted, string.Empty));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch
                {
                    for (var remaining = index; remaining < messages.Count; remaining++)
                        results.Add(new EventTrackingPublishResult(messages[remaining].EventId, EventTrackingPublishOutcome.SystemicFailure, "RabbitMqPublishFailed"));
                    break;
                }
            }
            return results;
        }
        finally { _gate.Release(); }
    }
    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            if (_channel is not null) { await _channel.DisposeAsync(); _channel = null; }
            if (_connection is not null) { await _connection.DisposeAsync(); _connection = null; }
            _connection = await _factory.CreateConnectionAsync(cancellationToken);
        }
        if (_channel is null || !_channel.IsOpen)
        {
            if (_channel is not null) await _channel.DisposeAsync();
            _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(true, true), cancellationToken);
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        }
        return _channel;
    }
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { if (_channel is not null) await _channel.DisposeAsync(); if (_connection is not null) await _connection.DisposeAsync(); }
        finally { _gate.Release(); _gate.Dispose(); }
    }
}
