using Identity.Domain.Entities;

namespace Identity.UnitTests;

public sealed class EmployeeInvitationDomainTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static EmployeeInvitation CreateInvitation() =>
        new(Guid.NewGuid(), "employee@example.com", "EMPLOYEE@EXAMPLE.COM",
            "New Employee", "org-1", "team-1", new string('A', 64),
            Guid.NewGuid(), CreatedAtUtc, CreatedAtUtc.AddHours(24));

    [Fact]
    public void NewInvitation_IsPendingAndUsesFirstDeliveryGeneration()
    {
        var invitation = CreateInvitation();

        Assert.Equal("Pending", invitation.Status);
        Assert.Equal("EMPLOYEE@EXAMPLE.COM", invitation.PendingNormalizedEmail);
        Assert.Equal(1, invitation.DeliveryGeneration);
        Assert.True(invitation.IsPendingAndUnexpired(CreatedAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void NewInvitation_AllowsMissingTeamAssignment()
    {
        var invitation = new EmployeeInvitation(
            Guid.NewGuid(), "employee@example.com", "EMPLOYEE@EXAMPLE.COM",
            "New Employee", "org-1", null, new string('A', 64),
            Guid.NewGuid(), CreatedAtUtc, CreatedAtUtc.AddHours(24));

        Assert.Null(invitation.TeamId);
    }

    [Fact]
    public void RotateToken_InvalidatesPreviousDigestAndAdvancesGeneration()
    {
        var invitation = CreateInvitation();
        var replacementDigest = new string('B', 64);

        invitation.RotateToken(replacementDigest, CreatedAtUtc.AddHours(1), CreatedAtUtc.AddHours(25));

        Assert.Equal(replacementDigest, invitation.TokenDigest);
        Assert.Equal(2, invitation.DeliveryGeneration);
        Assert.Equal(CreatedAtUtc.AddHours(25), invitation.ExpiresAtUtc);
    }

    [Fact]
    public void RotateToken_RejectsExpiredInvitation()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            invitation.RotateToken(new string('B', 64), CreatedAtUtc.AddHours(24), CreatedAtUtc.AddHours(48)));
    }

    [Fact]
    public void Accept_IsSingleUseAndReleasesPendingEmailUniqueness()
    {
        var invitation = CreateInvitation();

        Assert.True(invitation.Accept(CreatedAtUtc.AddHours(1)));
        Assert.False(invitation.Accept(CreatedAtUtc.AddHours(1)));
        Assert.Equal("Accepted", invitation.Status);
        Assert.Null(invitation.PendingNormalizedEmail);
        Assert.Null(invitation.TokenDigest);
        Assert.Equal(CreatedAtUtc.AddHours(1), invitation.AcceptedAtUtc);
    }

    [Fact]
    public void Cancel_IsTerminalAndPreventsAcceptance()
    {
        var invitation = CreateInvitation();

        Assert.True(invitation.Cancel(CreatedAtUtc.AddHours(1)));
        Assert.False(invitation.Cancel(CreatedAtUtc.AddHours(2)));
        Assert.False(invitation.Accept(CreatedAtUtc.AddHours(2)));
        Assert.Equal("Cancelled", invitation.Status);
        Assert.Null(invitation.PendingNormalizedEmail);
        Assert.Null(invitation.TokenDigest);
    }

    [Fact]
    public void ExpiredInvitation_CannotBeAcceptedOrResent()
    {
        var invitation = CreateInvitation();
        var expiredAt = CreatedAtUtc.AddHours(24);

        Assert.True(invitation.MarkExpired(expiredAt));
        Assert.False(invitation.Accept(expiredAt));
        Assert.Throws<InvalidOperationException>(() =>
            invitation.RotateToken(new string('B', 64), expiredAt, expiredAt.AddHours(24)));
        Assert.Equal("Expired", invitation.Status);
        Assert.Null(invitation.TokenDigest);
    }
}
