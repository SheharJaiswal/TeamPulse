using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using RabbitMQ.Client;

namespace Identity.Infrastructure.Messaging;

public sealed class RabbitMqPublisher : IEventPublisher, IAsyncDisposable
{
    private const int MaximumBatchSize = 10;
    private const string ConnectionName = "TeamPulse.Identity.OutboxPublisher";
    private readonly RabbitMqOptions _options;
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(RabbitMqOptions options)
    {
        options.Validate();
        _options = options;
        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(options.Uri, UriKind.Absolute),
            ClientProvidedName = ConnectionName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<IReadOnlyList<OutboxPublishResult>> PublishBatchAsync(
        IReadOnlyList<OutboxDispatchMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count is < 1 or > MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(messages));

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            IChannel channel;
            try
            {
                channel = await GetChannelAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return CreateSystemicResults(messages, "RabbitMqUnavailable");
            }

            var results = new List<OutboxPublishResult>(messages.Count);
            for (var index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                try
                {
                    var properties = new BasicProperties
                    {
                        MessageId = message.EventId.ToString("D"),
                        Type = message.EventType,
                        CorrelationId = message.CorrelationId.ToString("D"),
                        ContentType = "application/json",
                        DeliveryMode = 2,
                        Headers = new Dictionary<string, object?>
                        {
                            ["schemaVersion"] = message.SchemaVersion
                        }
                    };

                    await channel.BasicPublishAsync(
                        _options.ExchangeName, _options.RoutingKey, mandatory: true,
                        basicProperties: properties, body: Encoding.UTF8.GetBytes(message.Detail),
                        cancellationToken: cancellationToken);

                    results.Add(new OutboxPublishResult(
                        message.EventId, OutboxPublishOutcome.Accepted, string.Empty));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    for (var remaining = index; remaining < messages.Count; remaining++)
                        results.Add(new OutboxPublishResult(messages[remaining].EventId,
                            OutboxPublishOutcome.SystemicFailure, "RabbitMqPublishFailed"));
                    break;
                }
            }

            return results;
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
            _connection = await _connectionFactory.CreateConnectionAsync(ConnectionName, cancellationToken);
        }

        if (_channel is null || !_channel.IsOpen)
        {
            if (_channel is not null)
                await _channel.DisposeAsync();
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic,
                durable: true, autoDelete: false, cancellationToken: cancellationToken);
        }

        return _channel;
    }

    private static IReadOnlyList<OutboxPublishResult> CreateSystemicResults(
        IReadOnlyList<OutboxDispatchMessage> messages, string errorCode) =>
        messages.Select(message => new OutboxPublishResult(
            message.EventId, OutboxPublishOutcome.SystemicFailure, errorCode)).ToArray();

    public async ValueTask DisposeAsync()
    {
        await _publishGate.WaitAsync();
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _publishGate.Release();
            _publishGate.Dispose();
        }
    }
}
