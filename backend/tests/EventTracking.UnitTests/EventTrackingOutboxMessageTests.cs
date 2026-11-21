using System;
using EventTracking.Domain.Entities;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class EventTrackingOutboxMessageTests
{
    [Fact]
    public void Lease_CanBeAcquiredAndPublishedOnlyByOwner()
    {
        var now = DateTime.UtcNow;
        var message = new EventTrackingOutboxMessage(Guid.NewGuid(), "HabitActivityRecorded", 1,
            now, Guid.NewGuid(), "{}");
        var owner = Guid.NewGuid();
        Assert.True(message.TryAcquireLease(owner, now, now.AddSeconds(60)));
        Assert.False(message.TryMarkPublished(Guid.NewGuid(), now));
        Assert.True(message.TryMarkPublished(owner, now));
        Assert.False(message.TryAcquireLease(Guid.NewGuid(), now, now.AddSeconds(60)));
    }

    [Fact]
    public void Lease_CanBeRetriedAndQuarantinedWithSanitizedErrorCode()
    {
        var now = DateTime.UtcNow;
        var retryable = new EventTrackingOutboxMessage(Guid.NewGuid(), "HabitActivityRecorded", 1,
            now, Guid.NewGuid(), "{}");
        var firstOwner = Guid.NewGuid();
        Assert.True(retryable.TryAcquireLease(firstOwner, now, now.AddSeconds(60)));
        Assert.True(retryable.TryScheduleRetry(firstOwner, now, now.AddSeconds(10), "Broker unavailable / retry"));
        Assert.False(retryable.TryAcquireLease(Guid.NewGuid(), now, now.AddSeconds(5)));
        var secondOwner = Guid.NewGuid();
        Assert.True(retryable.TryAcquireLease(secondOwner, now.AddSeconds(11), now.AddSeconds(70)));
        Assert.True(retryable.TryQuarantine(secondOwner, now.AddSeconds(12), "Invalid event!"));
        Assert.False(retryable.TryAcquireLease(Guid.NewGuid(), now.AddSeconds(13), now.AddSeconds(80)));
    }
    [Fact]
    public void ExpiredLease_CannotBeCompletedByOriginalOwnerBeforeAnotherClaim()
    {
        var now = DateTime.UtcNow;
        var message = new EventTrackingOutboxMessage(Guid.NewGuid(), "HabitActivityRecorded", 1,
            now, Guid.NewGuid(), "{}");
        var owner = Guid.NewGuid();
        var expiresAt = now.AddSeconds(10);

        Assert.True(message.TryAcquireLease(owner, now, expiresAt));
        Assert.False(message.TryMarkPublished(owner, expiresAt));
        Assert.False(message.TryScheduleRetry(owner, expiresAt, expiresAt.AddSeconds(5), "BrokerUnavailable"));
        Assert.False(message.TryQuarantine(owner, expiresAt, "MalformedEvent"));
        Assert.Equal(owner, message.LeaseToken);
        Assert.Null(message.PublishedAtUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.DeadLetteredAtUtc);
    }

    [Fact]
    public void Lease_RejectsInvalidUtcAndNonPositiveDuration()
    {
        var now = DateTime.UtcNow;
        var message = new EventTrackingOutboxMessage(Guid.NewGuid(), "HabitActivityRecorded", 1,
            now, Guid.NewGuid(), "{}");

        Assert.False(message.TryAcquireLease(Guid.NewGuid(), DateTime.SpecifyKind(now, DateTimeKind.Unspecified),
            now.AddSeconds(60)));
        Assert.False(message.TryAcquireLease(Guid.NewGuid(), now, now));
        Assert.False(message.TryAcquireLease(Guid.NewGuid(), now, DateTime.SpecifyKind(now.AddSeconds(60), DateTimeKind.Local)));
        Assert.Equal(0, message.AttemptCount);
    }
}
