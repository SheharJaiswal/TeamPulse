using System.Threading;

namespace Challenge.Infrastructure.Messaging;

public sealed class ChallengeRabbitMqConsumerStatus
{
    private int _connected;
    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public void MarkConnected() => Interlocked.Exchange(ref _connected, 1);
    public void MarkDisconnected() => Interlocked.Exchange(ref _connected, 0);
}