using System.Threading;

namespace Challenge.Infrastructure.Messaging;

public sealed class ChallengeRabbitMqConsumerStatus
{
    private int _connected;
    private long _connectionAttemptCount;
    private long _establishedSessionCount;
    private long _completedSessionCount;

    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public long ConnectionAttemptCount => Interlocked.Read(ref _connectionAttemptCount);
    public long EstablishedSessionCount => Interlocked.Read(ref _establishedSessionCount);
    public long CompletedSessionCount => Interlocked.Read(ref _completedSessionCount);

    public void MarkConnectionAttempt() => Interlocked.Increment(ref _connectionAttemptCount);

    public void MarkConnected()
    {
        Interlocked.Exchange(ref _connected, 1);
        Interlocked.Increment(ref _establishedSessionCount);
    }

    public void MarkDisconnected() => Interlocked.Exchange(ref _connected, 0);
    public void MarkSessionEnded() => Interlocked.Increment(ref _completedSessionCount);
}