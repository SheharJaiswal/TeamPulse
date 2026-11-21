using System;

namespace Identity.Domain.Entities;

public sealed class EmployeeInvitation
{
    private EmployeeInvitation() { }

    public EmployeeInvitation(
        Guid id, string email, string normalizedEmail, string displayName,
        string organizationId, string teamId, string tokenDigest,
        Guid invitedByEmployeeId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invitation ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(normalizedEmail)) throw new ArgumentException("Normalized email is required.", nameof(normalizedEmail));
        if (string.IsNullOrWhiteSpace(tokenDigest) || tokenDigest.Length != 64)
            throw new ArgumentException("A SHA-256 token digest is required.", nameof(tokenDigest));
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("Expiry must be after creation.", nameof(expiresAtUtc));

        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PendingNormalizedEmail = normalizedEmail;
        DisplayName = displayName;
        OrganizationId = organizationId;
        TeamId = teamId;
        TokenDigest = tokenDigest;
        InvitedByEmployeeId = invitedByEmployeeId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = "Pending";
        DeliveryGeneration = 1;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? PendingNormalizedEmail { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string OrganizationId { get; private set; } = string.Empty;
    public string TeamId { get; private set; } = string.Empty;
    public string? TokenDigest { get; private set; }
    public Guid InvitedByEmployeeId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public int DeliveryGeneration { get; private set; }

    public bool IsPendingAndUnexpired(DateTime nowUtc) =>
        Status == "Pending" && ExpiresAtUtc > nowUtc;

    public void RotateToken(string tokenDigest, DateTime nowUtc, DateTime expiresAtUtc)
    {
        if (!IsPendingAndUnexpired(nowUtc))
            throw new InvalidOperationException("Only pending, unexpired invitations can be resent.");
        if (string.IsNullOrWhiteSpace(tokenDigest) || tokenDigest.Length != 64)
            throw new ArgumentException("A SHA-256 token digest is required.", nameof(tokenDigest));
        if (expiresAtUtc <= nowUtc)
            throw new ArgumentException("Expiry must be after the resend time.", nameof(expiresAtUtc));

        TokenDigest = tokenDigest;
        CreatedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
        DeliveryGeneration = checked(DeliveryGeneration + 1);
    }

    public bool Accept(DateTime nowUtc)
    {
        if (!IsPendingAndUnexpired(nowUtc)) return false;
        Status = "Accepted";
        AcceptedAtUtc = nowUtc;
        PendingNormalizedEmail = null;
        TokenDigest = null;
        return true;
    }

    public bool Cancel(DateTime nowUtc)
    {
        if (Status != "Pending" || ExpiresAtUtc <= nowUtc) return false;
        Status = "Cancelled";
        CancelledAtUtc = nowUtc;
        PendingNormalizedEmail = null;
        TokenDigest = null;
        return true;
    }

    public bool MarkExpired(DateTime nowUtc)
    {
        if (Status != "Pending" || ExpiresAtUtc > nowUtc) return false;
        Status = "Expired";
        PendingNormalizedEmail = null;
        TokenDigest = string.Empty;
        return true;
    }
}
