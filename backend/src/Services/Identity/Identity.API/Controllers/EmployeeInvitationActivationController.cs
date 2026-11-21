#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Identity.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/invitations")]
[EnableRateLimiting("invitation-activation")]
public sealed class EmployeeInvitationActivationController(
    IEmployeeInvitationActivationService activationService,
    ILogger<EmployeeInvitationActivationController> logger) : ControllerBase
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

        EmployeeInvitationActivationResult result;
        try
        {
            result = await activationService.ActivateAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Employee invitation activation failed unexpectedly. ErrorType={ErrorType}",
                exception.GetType().Name);
            throw;
        }

        return result.Status == EmployeeInvitationActivationStatus.Activated
            ? Ok(new { message = "Your account is active. Sign in to continue." })
            : BadRequest(new { message = "This invitation is invalid or can no longer be used." });
    }
}
