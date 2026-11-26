using System;

namespace Challenge.Domain.Entities;

public sealed class ChallengeInvitation
{
    private ChallengeInvitation() { }

    public ChallengeInvitation(Guid id, Guid challengeId, Guid employeeId, string email, string displayName, DateTime invitedAtUtc)
    {
        if (id == Guid.Empty || challengeId == Guid.Empty || employeeId == Guid.Empty)
            throw new ArgumentException("Invitation identifiers are required.");
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
            throw new ArgumentException("A valid employee email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150)
            throw new ArgumentException("Employee display name is required.", nameof(displayName));
        if (invitedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Invitation time must be UTC.", nameof(invitedAtUtc));
        Id = id;
        ChallengeId = challengeId;
        EmployeeId = employeeId;
        Email = email.Trim();
        DisplayName = displayName.Trim();
        InvitedAtUtc = invitedAtUtc;
        EmailDeliveryStatus = "Pending";
    }

    public Guid Id { get; private set; }
    public Guid ChallengeId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTime InvitedAtUtc { get; private set; }
    public DateTime? EmailSentAtUtc { get; private set; }
    public string EmailDeliveryStatus { get; private set; } = "Pending";

    public void MarkEmailSent(DateTime sentAtUtc)
    {
        if (sentAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Email sent time must be UTC.", nameof(sentAtUtc));
        EmailSentAtUtc = sentAtUtc;
        EmailDeliveryStatus = "Sent";
    }

    public void MarkEmailFailed()
    {
        EmailDeliveryStatus = "Failed";
    }
}