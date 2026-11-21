using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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
        IReadOnlyBasicProperties properties,
        ReadOnlyMemory<byte> body)
    {
        _returned = false;

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: destination,
            mandatory: true,
            basicProperties: properties,
            body: body);

        // RabbitMQ sends Basic.Return before the publisher confirmation for an unroutable
        // mandatory publication. BasicPublishAsync tracks the confirmation on this channel.
        if (_returned)
        {
            throw new InvalidOperationException(
                "The broker returned a mandatory destination publication.");
        }
    }
}
