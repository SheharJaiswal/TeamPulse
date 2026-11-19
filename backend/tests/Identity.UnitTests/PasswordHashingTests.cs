using Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.UnitTests;

public sealed class PasswordHashingTests
{
    private readonly PasswordHasher<Employee> _hasher = new();
    private readonly Employee _employee = new(
        Guid.NewGuid(), "employee@teampulse.test", "EMPLOYEE@TEAMPULSE.TEST",
        "Test Employee", "not-yet-hashed", "org-test", "team-test", "Employee");

    [Fact]
    public void HashPassword_ShouldNotStoreThePlainTextPassword()
    {
        const string password = "Strong-Test-Password-123!";
        var hash = _hasher.HashPassword(_employee, password);
        Assert.NotEqual(password, hash);
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void VerifyHashedPassword_ShouldAcceptOriginalPassword()
    {
        const string password = "Strong-Test-Password-123!";
        var hash = _hasher.HashPassword(_employee, password);
        var result = _hasher.VerifyHashedPassword(_employee, hash, password);
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_ShouldRejectDifferentPassword()
    {
        var hash = _hasher.HashPassword(_employee, "Strong-Test-Password-123!");
        var result = _hasher.VerifyHashedPassword(_employee, hash, "Different-Password-456!");
        Assert.Equal(PasswordVerificationResult.Failed, result);
    }
}
