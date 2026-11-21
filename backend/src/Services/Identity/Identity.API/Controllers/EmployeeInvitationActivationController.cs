#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/invitations")]
[EnableRateLimiting("invitation-activation")]
public sealed class EmployeeInvitationActivationController(
    IEmployeeInvitationActivationService activationService) : ControllerBase
{
    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateEmployeeInvitationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 12 ||
            request.Password.Length > 256)
            return BadRequest(new { message = "A valid invitation token and a password of 12–256 characters are required." });

        var result = await activationService.ActivateAsync(request, cancellationToken);
        return result.Status == EmployeeInvitationActivationStatus.Activated
            ? Ok(new { message = "Your account is active. Sign in to continue." })
            : BadRequest(new { message = "This invitation is invalid or can no longer be used." });
    }
}
