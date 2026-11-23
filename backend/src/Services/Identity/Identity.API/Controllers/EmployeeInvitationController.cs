#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Authorize(Roles = "HrAdmin")]
[Route("api/identity/invitations")]
public sealed class EmployeeInvitationController(
    IEmployeeInvitationService invitationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmployeeInvitationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorEmployeeId) ||
            actorEmployeeId == Guid.Empty)
            return Unauthorized();

        if (!IsValid(request))
            return BadRequest(new
            {
                message = "Email must be valid; displayName and organizationId are required; teamId is optional. All supplied values must fit their field limits."
            });

        var result = await invitationService.CreateAsync(
            actorEmployeeId, request!, cancellationToken);

        return result.Status switch
        {
            EmployeeInvitationCommandStatus.Created =>
                CreatedAtAction(nameof(Get), new { invitationId = result.Invitation!.InvitationId }, result.Invitation),
            EmployeeInvitationCommandStatus.Conflict =>
                Conflict(new { message = "An employee or pending invitation already exists for this email." }),
            _ => StatusCode(500)
        };
    }

    [HttpGet("{invitationId:guid}")]
    public async Task<IActionResult> Get(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitationService.GetAsync(invitationId, cancellationToken);
        return invitation is null
            ? NotFound(new { message = "Invitation was not found." })
            : Ok(invitation);
    }

    [HttpPost("{invitationId:guid}/resend")]
    public async Task<IActionResult> Resend(Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await invitationService.ResendAsync(invitationId, cancellationToken);
        return result.Status switch
        {
            EmployeeInvitationCommandStatus.Updated => Ok(result.Invitation),
            EmployeeInvitationCommandStatus.NotFound => NotFound(new { message = "Invitation was not found." }),
            EmployeeInvitationCommandStatus.Expired => StatusCode(410, new { message = "Invitation has expired." }),
            EmployeeInvitationCommandStatus.AlreadyTerminal => Conflict(new { message = "Only pending invitations can be resent." }),
            _ => StatusCode(500)
        };
    }

    [HttpPost("{invitationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await invitationService.CancelAsync(invitationId, cancellationToken);
        return result.Status switch
        {
            EmployeeInvitationCommandStatus.Updated => Ok(result.Invitation),
            EmployeeInvitationCommandStatus.NotFound => NotFound(new { message = "Invitation was not found." }),
            EmployeeInvitationCommandStatus.Expired => StatusCode(410, new { message = "Invitation has expired." }),
            EmployeeInvitationCommandStatus.AlreadyTerminal => Conflict(new { message = "Only pending invitations can be cancelled." }),
            _ => StatusCode(500)
        };
    }

    private static bool IsValid(CreateEmployeeInvitationRequest? request)
    {
        if (request is null ||
            !HasLength(request.Email, 320) ||
            !HasLength(request.DisplayName, 150) ||
            !HasLength(request.OrganizationId, 100) ||
            !HasOptionalLength(request.TeamId, 100))
            return false;

        try
        {
            var address = new MailAddress(request.Email.Trim());
            return string.Equals(address.Address, request.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HasLength(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;

    private static bool HasOptionalLength(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximum;
}
