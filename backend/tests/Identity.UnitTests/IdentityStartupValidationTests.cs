namespace Identity.UnitTests;

public sealed class IdentityStartupValidationTests
{
    private const string SigningKeyError =
        "JWT signing key must be configured and at least 32 UTF-8 bytes.";

    [Fact]
    public void Startup_WhenSigningKeyIsMissing_FailsFastWithConfigurationError()
    {
        using var factory = new IdentityStartupValidationFactory(includeSigningKey: false);

        var details = CaptureStartupFailure(factory);

        Assert.Contains(SigningKeyError, details);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("                                ")]
    public void Startup_WhenSigningKeyIsBlankOrShorterThanThirtyTwoBytes_FailsFast(
        string signingKey)
    {
        using var factory = new IdentityStartupValidationFactory(
            includeSigningKey: true,
            signingKey);

        var details = CaptureStartupFailure(factory);

        Assert.Contains(SigningKeyError, details);
    }

    [Fact]
    public void Startup_WhenSigningKeyIsExactlyThirtyTwoBytes_PassesKeyValidation()
    {
        using var factory = new IdentityStartupValidationFactory(
            includeSigningKey: true,
            new string('k', 32));

        var details = CaptureStartupFailure(factory);

        // The test intentionally omits the database connection string. Reaching this
        // later validation proves the 32-byte signing key passed the JWT startup guard.
        Assert.Contains("IdentityDatabase connection string is missing.", details);
        Assert.DoesNotContain("JWT signing key", details);
    }

    private static string CaptureStartupFailure(IdentityStartupValidationFactory factory)
    {
        var exception = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(exception);
        return exception!.ToString();
    }
}
