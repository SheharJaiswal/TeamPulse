using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Challenge.UnitTests;

public sealed class ChallengeApiFactory(
    string connectionString,
    bool consumerEnabled = false,
    string? rabbitMqUri = null) : WebApplicationFactory<Program>
{
    private const string TestSigningKey = "challenge-integration-test-signing-key-at-least-32-bytes";

    public static string CreateToken(System.Guid employeeId, string organizationId, string role)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", employeeId.ToString()),
            new System.Security.Claims.Claim("organization_id", organizationId),
            new System.Security.Claims.Claim("role", role)
        };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "TeamPulse.Identity", audience: "TeamPulse.Services", claims: claims,
            notBefore: System.DateTime.UtcNow.AddMinutes(-1),
            expires: System.DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ChallengeDatabase", connectionString);
        builder.UseSetting("ChallengeConsumer:Enabled", consumerEnabled ? "true" : "false");
        builder.UseSetting("ChallengeConsumer:Uri", rabbitMqUri ?? string.Empty);
        builder.UseSetting("ChallengeConsumer:ReconnectDelayMilliseconds", "250");
        builder.UseSetting("ChallengeConsumer:ConnectionTimeoutMilliseconds", "1000");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ChallengeDatabase"] = connectionString,
                ["ChallengeConsumer:Enabled"] = consumerEnabled ? "true" : "false",
                ["ChallengeConsumer:Uri"] = rabbitMqUri ?? string.Empty,
                ["ChallengeConsumer:ReconnectDelayMilliseconds"] = "250",
                ["ChallengeConsumer:ConnectionTimeoutMilliseconds"] = "1000",
                ["Jwt:SigningKey"] = TestSigningKey
            });
        });
    }
}
