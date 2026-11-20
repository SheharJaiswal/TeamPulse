using Identity.API.Security;

namespace Identity.UnitTests;

public sealed class IdentityStartupValidationTests
{
    private const string SigningKeyError =
        "JWT signing key must be configured and at least 32 UTF-8 bytes.";

    [Fact]
    public void Startup_WhenSigningKeyIsNotConfigured_FailsBeforeDatabaseStartup()
    {
        using var factory = new IdentityStartupValidationFactory(signingKey: null);

        var details = CaptureStartupFailure(factory);

        Assert.Contains(SigningKeyError, details);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("                                ")]
    public void Startup_WhenSigningKeyIsBlankOrShorterThanThirtyTwoBytes_FailsFast(
        string signingKey)
    {
        using var factory = new IdentityStartupValidationFactory(signingKey);

        var details = CaptureStartupFailure(factory);

        Assert.Contains(SigningKeyError, details);
    }

    [Fact]
    public void SigningKeyValidator_AcceptsExactlyThirtyTwoUtf8Bytes()
    {
        // Sixteen U+00E9 characters occupy 32 bytes in UTF-8.
        var signingKey = new string('é', 16);

        Assert.Equal(32, System.Text.Encoding.UTF8.GetByteCount(signingKey));
        var exception = Record.Exception(() => JwtSigningKeyValidator.Validate(signingKey));

        Assert.Null(exception);
    }

    [Fact]
    public void SigningKeyValidator_RejectsThirtyOneUtf8Bytes()
    {
        var signingKey = string.Concat(new string('é', 15), "a");

        Assert.Equal(31, System.Text.Encoding.UTF8.GetByteCount(signingKey));
        Assert.Throws<InvalidOperationException>(() => JwtSigningKeyValidator.Validate(signingKey));
    }

    private static string CaptureStartupFailure(IdentityStartupValidationFactory factory)
    {
        var exception = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(exception);
        return exception!.ToString();
    }
}
