using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/identity")]
public sealed class IdentityController(
    IEmployeeRepository employees,
    IPasswordHasher<Employee> passwordHasher,
    ITokenService tokenService,
    IHostEnvironment hostEnvironment,
    IOptions<JwtOptions> jwtOptions):ControllerBase
{
    // Development bootstrap only; replace with HR invitation/provisioning before production.
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request,CancellationToken ct)
    {
        // This endpoint is a local bootstrap only; never expose self-registration in hosted environments.
        if (!hostEnvironment.IsDevelopment())
            return NotFound();

        if(string.IsNullOrWhiteSpace(request.Email)||string.IsNullOrWhiteSpace(request.DisplayName)||
           string.IsNullOrWhiteSpace(request.OrganizationId)||string.IsNullOrWhiteSpace(request.TeamId)||
           string.IsNullOrWhiteSpace(request.Password)||request.Password.Length<12)
            return BadRequest(new{message="All fields are required and password must contain at least 12 characters."});

        var email=request.Email.Trim();
        var normalized=email.ToUpperInvariant();
        if(await employees.GetByNormalizedEmailAsync(normalized,ct) is not null)
            return Conflict(new{message="An employee with this email already exists."});

        var id=Guid.NewGuid();
        var hashContext=new Employee(id,email,normalized,request.DisplayName.Trim(),"temporary",
            request.OrganizationId.Trim(),request.TeamId.Trim(),"Employee");
        var hash=passwordHasher.HashPassword(hashContext,request.Password);
        var employee=new Employee(id,email,normalized,request.DisplayName.Trim(),hash,
            request.OrganizationId.Trim(),request.TeamId.Trim(),"Employee");
        await employees.AddAsync(employee,ct);
        await employees.SaveChangesAsync(ct);
        return Ok(CreateResponse(employee));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.Email)||string.IsNullOrEmpty(request.Password))
            return BadRequest(new{message="Email and password are required."});
        var employee=await employees.GetByNormalizedEmailAsync(request.Email.Trim().ToUpperInvariant(),ct);
        if (employee is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = passwordHasher.VerifyHashedPassword(employee, employee.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        // Verify the supplied password before checking status, but never rehash or issue a token for inactive accounts.
        if (!employee.IsActive)
            return Unauthorized(new { message = "Invalid email or password." });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Persist the upgraded hash before issuing a token. If persistence fails, login fails closed.
            var upgradedHash = passwordHasher.HashPassword(employee, request.Password);
            employee.UpdatePasswordHash(upgradedHash);
            await employees.SaveChangesAsync(ct);
        }
        else if (result != PasswordVerificationResult.Success)
        {
            // Treat unknown hasher results as authentication failures.
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(CreateResponse(employee));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()=>Ok(new{
        employeeId=User.FindFirst("sub")?.Value,
        email=User.FindFirst("email")?.Value,
        displayName=User.FindFirst("name")?.Value,
        organizationId=User.FindFirst("organization_id")?.Value,
        teamId=User.FindFirst("team_id")?.Value,
        role=User.FindFirst("role")?.Value});

    private AuthResponse CreateResponse(Employee employee)
    {
        var expires=DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes);
        return new AuthResponse(tokenService.CreateAccessToken(employee),"Bearer",expires,
            new EmployeeSummary(employee.Id,employee.Email,employee.DisplayName,
                employee.OrganizationId,employee.TeamId,employee.Role));
    }
}