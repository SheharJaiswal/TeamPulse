using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Authorize(Roles = "HrAdmin")]
[Route("api/identity/employees/{employeeId:guid}")]
public sealed class EmployeeLifecycleController(IEmployeeLifecycleService lifecycleService) : ControllerBase
{
    [HttpPost("deactivate")]
    public Task<IActionResult> Deactivate(
        Guid employeeId, EmployeeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(employeeId, request, desiredIsActive: false, cancellationToken);

    [HttpPost("reactivate")]
    public Task<IActionResult> Reactivate(
        Guid employeeId, EmployeeLifecycleRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(employeeId, request, desiredIsActive: true, cancellationToken);

    private async Task<IActionResult> ExecuteAsync(
        Guid employeeId,
        EmployeeLifecycleRequest request,
        bool desiredIsActive,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actorEmployeeId))
            return Unauthorized();

        var idempotencyValues = Request.Headers["Idempotency-Key"];
        var idempotencyKey = idempotencyValues.Count == 1 ? idempotencyValues[0] ?? string.Empty : string.Empty;
        if (idempotencyValues.Count != 1 || string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128 ||
            idempotencyKey.Any(character => character < 0x20 || character > 0x7e))
        {
            return BadRequest(new { message = "Idempotency-Key must contain 1–128 visible ASCII characters." });
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ReasonCode) ||
            request.ReasonCode.Trim().Length > 100 || request.Note?.Length > 1000)
        {
            return BadRequest(new { message = "A reasonCode (1–100 characters) is required; note must not exceed 1000 characters." });
        }

        var hasCorrelationHeader = Request.Headers.ContainsKey("X-Correlation-ID");
        var correlationValues = Request.Headers["X-Correlation-ID"];
        Guid correlationId;
        if (!hasCorrelationHeader)
        {
            correlationId = Guid.NewGuid();
        }
        else if (correlationValues.Count != 1 ||
                 !Guid.TryParse(correlationValues[0], out correlationId))
        {
            return BadRequest(new { message = "X-Correlation-ID must be a UUID." });
        }

        var result = await lifecycleService.ExecuteAsync(
            employeeId,
            actorEmployeeId,
            desiredIsActive,
            idempotencyKey,
            request.ReasonCode,
            request.Note,
            correlationId,
            cancellationToken);

        return result.Status switch
        {
            EmployeeLifecycleCommandStatus.NotFound => NotFound(new { message = "Employee was not found." }),
            EmployeeLifecycleCommandStatus.Conflict => Conflict(new { message = "The idempotency key conflicts with another command or the employee changed concurrently." }),
            _ => Ok(new
            {
                outcome = result.Status switch
                {
                    EmployeeLifecycleCommandStatus.Transitioned => "transitioned",
                    EmployeeLifecycleCommandStatus.AlreadyInDesiredState => "alreadyInDesiredState",
                    EmployeeLifecycleCommandStatus.Replayed => "replayed",
                    _ => "unknown"
                },
                employeeId = result.EmployeeId,
                isActive = result.IsActive,
                lifecycleVersion = result.LifecycleVersion,
                eventId = result.EventId,
                correlationId = result.CorrelationId
            })
        };
    }
}
