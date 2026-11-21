using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Identity.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.Infrastructure.Security;

public sealed class InvitationTokenService : IInvitationTokenService
{
    private const int TokenByteLength = 32;
    private readonly IDataProtector _protector;

    public InvitationTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("TeamPulse.Identity.InvitationDelivery.v1");
    }

    public InvitationTokenMaterial Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var protectedToken = _protector.Protect(token);
        CryptographicOperations.ZeroMemory(bytes);
        return new InvitationTokenMaterial(token, digest, protectedToken);
    }

    public string ComputeDigest(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43 ||
            token.Any(character => !((character >= 'A' && character <= 'Z') ||
                                     (character >= 'a' && character <= 'z') ||
                                     (character >= '0' && character <= '9') ||
                                     character == '-' || character == '_')))
            throw new ArgumentException("Invitation token is invalid.", nameof(token));

        byte[] bytes;
        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invitation token is invalid.", nameof(token));
        }

        if (bytes.Length != TokenByteLength)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new ArgumentException("Invitation token is invalid.", nameof(token));
        }

        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        CryptographicOperations.ZeroMemory(bytes);
        return digest;
    }

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
