using System.Threading;

namespace Challenge.Infrastructure.Messaging;

public sealed class ChallengeRabbitMqConsumerStatus
{
    private int _connected;
    private long _completedSessionCount;

    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public long CompletedSessionCount => Interlocked.Read(ref _completedSessionCount);

    public void MarkConnected() => Interlocked.Exchange(ref _connected, 1);
    public void MarkDisconnected() => Interlocked.Exchange(ref _connected, 0);
    public void MarkSessionEnded() => Interlocked.Increment(ref _completedSessionCount);
}