using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Authorize(Roles = "HrAdmin")]
[Route("api/identity/employees")]
public sealed class EmployeeDirectoryController(IEmployeeRepository employees) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var organizationId = User.FindFirst("organization_id")?.Value;
        if (string.IsNullOrWhiteSpace(organizationId)) return Unauthorized();

        var results = await employees.GetActiveByOrganizationAsync(organizationId, cancellationToken);
        return Ok(results.Select(employee => new ActiveEmployeeResponse(
            employee.Id, employee.Email, employee.DisplayName, employee.OrganizationId)));
    }

    public sealed record ActiveEmployeeResponse(Guid EmployeeId, string Email, string DisplayName, string OrganizationId);
}