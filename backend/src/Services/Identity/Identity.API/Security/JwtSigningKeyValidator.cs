using System.Text;

namespace Identity.API.Security;

public static class JwtSigningKeyValidator
{
    public const int MinimumKeyLengthBytes = 32;

    public static void Validate(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) ||
            Encoding.UTF8.GetByteCount(signingKey) < MinimumKeyLengthBytes)
        {
            throw new InvalidOperationException(
                "JWT signing key must be configured and at least 32 UTF-8 bytes.");
        }
    }
}
