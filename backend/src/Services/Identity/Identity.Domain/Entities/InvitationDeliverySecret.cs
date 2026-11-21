using System;

namespace Identity.Domain.Entities;

public sealed class InvitationDeliverySecret
{
    private InvitationDeliverySecret() { }

    public InvitationDeliverySecret(Guid id, Guid invitationId, int generation, string protectedToken, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(protectedToken)) throw new ArgumentException("Protected token is required.", nameof(protectedToken));
        Id = id;
        InvitationId = invitationId;
        Generation = generation;
        ProtectedToken = protectedToken;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid InvitationId { get; private set; }
    public int Generation { get; private set; }
    public string ProtectedToken { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}
