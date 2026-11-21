using System.Security.Cryptography;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.UnitTests;

public sealed class InvitationTokenServiceTests
{
    [Fact]
    public void Create_ProducesRandomUrlSafeTokenDigestAndRecoverableProtectedValue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "teampulse-identity-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var service = new InvitationTokenService(DataProtectionProvider.Create(new DirectoryInfo(directory)));

            var material = service.Create();

            Assert.Equal(43, material.Token.Length);
            Assert.DoesNotContain("+", material.Token);
            Assert.DoesNotContain("/", material.Token);
            Assert.DoesNotContain("=", material.Token);
            Assert.Equal(64, material.TokenDigest.Length);
            Assert.Equal(material.TokenDigest, service.ComputeDigest(material.Token));
            Assert.Equal(material.Token, service.Unprotect(material.ProtectedToken));
            Assert.NotEqual(material.Token, material.ProtectedToken);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ComputeDigest_RejectsMalformedAndWrongLengthTokens()
    {
        var directory = Path.Combine(Path.GetTempPath(), "teampulse-identity-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var service = new InvitationTokenService(DataProtectionProvider.Create(new DirectoryInfo(directory)));

            Assert.Throws<ArgumentException>(() => service.ComputeDigest("not-a-token"));
            Assert.Throws<ArgumentException>(() => service.ComputeDigest("YQ"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProtectedToken_CannotBeUnprotectedWithDifferentPurpose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "teampulse-identity-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var provider = DataProtectionProvider.Create(new DirectoryInfo(directory));
            var service = new InvitationTokenService(provider);
            var material = service.Create();
            var unrelatedProtector = provider.CreateProtector("Different.Purpose");

            Assert.Throws<CryptographicException>(() => unrelatedProtector.Unprotect(material.ProtectedToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
