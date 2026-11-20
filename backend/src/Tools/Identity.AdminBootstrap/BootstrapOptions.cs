using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Identity.AdminBootstrap;

public sealed class BootstrapOptions
{
    public BootstrapOptions(
        string email,
        string displayName,
        string organizationId,
        string teamId,
        string password)
    {
        Email = email;
        DisplayName = displayName;
        OrganizationId = organizationId;
        TeamId = teamId;
        Password = password;
    }

    public string Email { get; }
    public string DisplayName { get; }
    public string OrganizationId { get; }
    public string TeamId { get; }
    public string Password { get; }

    public static bool TryLoad(
        IConfiguration configuration,
        out BootstrapOptions? options,
        out string error)
    {
        options = null;
        var email = configuration["IdentityBootstrap:HrAdmin:Email"]?.Trim();
        var displayName = configuration["IdentityBootstrap:HrAdmin:DisplayName"]?.Trim();
        var organizationId = configuration["IdentityBootstrap:HrAdmin:OrganizationId"]?.Trim();
        var teamId = configuration["IdentityBootstrap:HrAdmin:TeamId"]?.Trim();
        var password = configuration["IdentityBootstrap:HrAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 ||
            !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            error = "A valid HR administrator email is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 150 ||
            string.IsNullOrWhiteSpace(organizationId) || organizationId.Length > 100 ||
            string.IsNullOrWhiteSpace(teamId) || teamId.Length > 100)
        {
            error = "Display name, organization ID and team ID are required and must fit Identity limits.";
            return false;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 12)
        {
            error = "The HR administrator password must contain at least 12 characters.";
            return false;
        }

        options = new BootstrapOptions(email, displayName, organizationId, teamId, password);
        error = string.Empty;
        return true;
    }
}
