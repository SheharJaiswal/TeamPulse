using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Insight.QueueMigration;

internal sealed class MandatoryPublisher
{
    private readonly IChannel _channel;
    private bool _returned;

    public MandatoryPublisher(IChannel channel)
    {
        _channel = channel;
        _channel.BasicReturnAsync += (_, _) =>
        {
            _returned = true;
            return Task.CompletedTask;
        };
    }

    public async Task PublishAsync(
        string destination,
        BasicProperties properties,
        ReadOnlyMemory<byte> body)
    {
        _returned = false;

        try
        {
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: destination,
                mandatory: true,
                basicProperties: properties,
                body: body);
        }
        catch (PublishReturnException exception)
        {
            // RabbitMQ.Client 7.2 surfaces a mandatory Basic.Return directly from
            // BasicPublishAsync while tracking publisher confirmations.
            _returned = false;
            throw new InvalidOperationException(
                "The broker returned a mandatory destination publication.",
                exception);
        }

        // Retain the event guard for clients/broker paths that report the return
        // through BasicReturnAsync without throwing from BasicPublishAsync.
        if (_returned)
        {
            throw new InvalidOperationException(
                "The broker returned a mandatory destination publication.");
        }
    }
}
