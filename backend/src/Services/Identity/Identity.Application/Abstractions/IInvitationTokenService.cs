namespace Identity.Application.Abstractions;

public sealed record InvitationTokenMaterial(string Token, string TokenDigest, string ProtectedToken);

public interface IInvitationTokenService
{
    InvitationTokenMaterial Create();
    string ComputeDigest(string token);
    string Unprotect(string protectedToken);
}
