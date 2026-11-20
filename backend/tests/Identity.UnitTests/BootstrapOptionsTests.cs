using Identity.AdminBootstrap;
using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

public sealed class BootstrapOptionsTests
{
    [Fact]
    public void TryLoad_WithValidSecretBackedConfiguration_ReturnsOptions()
    {
        var configuration = CreateConfiguration("Strong-HR-Admin-Password-123!");

        var valid = BootstrapOptions.TryLoad(configuration, out var options, out var error);

        Assert.True(valid);
        Assert.Empty(error);
        Assert.NotNull(options);
        Assert.Equal("hr.admin@teampulse.test", options.Email);
        Assert.Equal("HrAdmin", HrAdminBootstrapper.HrAdminRole);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void TryLoad_WithMissingOrShortPassword_FailsWithoutReturningOptions(string password)
    {
        var configuration = CreateConfiguration(password);

        var valid = BootstrapOptions.TryLoad(configuration, out var options, out var error);

        Assert.False(valid);
        Assert.Null(options);
        Assert.Contains("password", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryLoad_WithMissingOrganization_FailsWithoutReturningOptions()
    {
        var values = Values("Strong-HR-Admin-Password-123!");
        values["IdentityBootstrap:HrAdmin:OrganizationId"] = string.Empty;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var valid = BootstrapOptions.TryLoad(configuration, out var options, out _);

        Assert.False(valid);
        Assert.Null(options);
    }

    private static IConfiguration CreateConfiguration(string password) =>
        new ConfigurationBuilder().AddInMemoryCollection(Values(password)).Build();

    private static Dictionary<string, string?> Values(string password) => new()
    {
        ["IdentityBootstrap:HrAdmin:Email"] = "hr.admin@teampulse.test",
        ["IdentityBootstrap:HrAdmin:DisplayName"] = "TeamPulse HR Administrator",
        ["IdentityBootstrap:HrAdmin:OrganizationId"] = "org-test",
        ["IdentityBootstrap:HrAdmin:TeamId"] = "team-test",
        ["IdentityBootstrap:HrAdmin:Password"] = password
    };
}
