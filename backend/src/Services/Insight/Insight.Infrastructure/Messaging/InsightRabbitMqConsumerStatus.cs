using System;
using System.Collections.Generic;
using System.Threading;

namespace Insight.Infrastructure.Messaging;

public sealed class InsightRabbitMqConsumerStatus
{
    private int _connected;
    private long _connectionAttemptCount;
    private long _establishedSessionCount;
    private long _completedSessionCount;
    private string _currentSessionStage = "idle";
    private string _lastSessionFailureStage = "none";
    private string _lastSessionFailureTypes = "none";

    public bool IsConnected => Volatile.Read(ref _connected) == 1;
    public long ConnectionAttemptCount => Interlocked.Read(ref _connectionAttemptCount);
    public long EstablishedSessionCount => Interlocked.Read(ref _establishedSessionCount);
    public long CompletedSessionCount => Interlocked.Read(ref _completedSessionCount);
    public string CurrentSessionStage => Volatile.Read(ref _currentSessionStage);
    public string LastSessionFailureStage => Volatile.Read(ref _lastSessionFailureStage);
    public string LastSessionFailureTypes => Volatile.Read(ref _lastSessionFailureTypes);

    public void MarkConnectionAttempt()
    {
        Interlocked.Increment(ref _connectionAttemptCount);
        Interlocked.Exchange(ref _currentSessionStage, "connection");
        Interlocked.Exchange(ref _lastSessionFailureStage, "none");
        Interlocked.Exchange(ref _lastSessionFailureTypes, "none");
    }

    public void MarkSessionStage(string stage) =>
        Interlocked.Exchange(ref _currentSessionStage, stage);

    public void RecordSessionFailure(Exception exception)
    {
        Interlocked.Exchange(ref _lastSessionFailureStage, CurrentSessionStage);

        var exceptionTypes = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            exceptionTypes.Add(current.GetType().Name);

        Interlocked.Exchange(ref _lastSessionFailureTypes, string.Join(" -> ", exceptionTypes));
    }

    public void MarkConnected()
    {
        Interlocked.Exchange(ref _connected, 1);
        Interlocked.Increment(ref _establishedSessionCount);
        Interlocked.Exchange(ref _currentSessionStage, "connected");
    }

    public void MarkDisconnected() => Interlocked.Exchange(ref _connected, 0);
    public void MarkSessionEnded() => Interlocked.Increment(ref _completedSessionCount);
}