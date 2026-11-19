using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Identity.Infrastructure.Security;
public sealed class JwtTokenService(IOptions<JwtOptions> options):ITokenService
{
    private readonly JwtOptions _options=options.Value;
    public string CreateAccessToken(Employee employee)
    {
        var claims=new[]{
            new Claim(JwtRegisteredClaimNames.Sub,employee.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email,employee.Email),
            new Claim("name",employee.DisplayName),
            new Claim("organization_id",employee.OrganizationId),
            new Claim("team_id",employee.TeamId),
            new Claim("role",employee.Role)};
        var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds=new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
        var token=new JwtSecurityToken(issuer:_options.Issuer,audience:_options.Audience,claims:claims,
            expires:DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),signingCredentials:creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}