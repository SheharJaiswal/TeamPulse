using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.UnitTests;

public sealed class JwtTokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-with-at-least-32-bytes";
    private const string Issuer = "TeamPulse.Identity.Tests";
    private const string Audience = "TeamPulse.Services.Tests";

    [Fact]
    public void CreateAccessToken_ShouldProduceSignedTokenWithExpectedClaimsAndLifetime()
    {
        var employeeId = Guid.NewGuid();
        var employee = new Employee(employeeId, "employee@teampulse.test", "EMPLOYEE@TEAMPULSE.TEST",
            "Test Employee", "password-hash", "org-123", "team-456", "Employee");
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = Issuer, Audience = Audience, SigningKey = SigningKey, AccessTokenMinutes = 15
        }));

        var tokenText = service.CreateAccessToken(employee);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(tokenText, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Issuer,
            ValidateAudience = true, ValidAudience = Audience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal(employeeId.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(employee.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(employee.DisplayName, principal.FindFirst("name")?.Value);
        Assert.Equal(employee.OrganizationId, principal.FindFirst("organization_id")?.Value);
        Assert.Equal(employee.TeamId, principal.FindFirst("team_id")?.Value);
        Assert.Equal(employee.Role, principal.FindFirst("role")?.Value);

        var jwt = (JwtSecurityToken)validatedToken;
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void CreateAccessToken_ShouldNotValidateWithAnIncorrectSigningKey()
    {
        var employee = new Employee(Guid.NewGuid(), "employee@teampulse.test", "EMPLOYEE@TEAMPULSE.TEST",
            "Test Employee", "password-hash", "org-123", "team-456", "Employee");
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = Issuer, Audience = Audience, SigningKey = SigningKey, AccessTokenMinutes = 15
        }));
        var token = service.CreateAccessToken(employee);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Issuer,
                ValidateAudience = true, ValidAudience = Audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("different-test-signing-key-with-32-bytes")),
                ClockSkew = TimeSpan.Zero
            }, out _));
    }
}
