using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Habit.Application.Abstractions;
using Habit.Application.Contracts;
using Habit.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/habits")]
public sealed class HabitsController(IHabitService habits) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] HabitStatus? status, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest(new { message = "Unknown habit status." });
        try
        {
            return Ok(await habits.GetMineAsync(employeeId, status, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        try
        {
            var result = await habits.GetAsync(employeeId, id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHabitRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        try
        {
            var created = await habits.CreateAsync(employeeId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateHabitRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        try
        {
            var updated = await habits.UpdateAsync(employeeId, id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        try
        {
            return await habits.ArchiveAsync(employeeId, id, cancellationToken) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    private bool TryGetEmployeeId(out Guid employeeId)
    {
        var subject = User.FindFirstValue("sub");
        return Guid.TryParse(subject, out employeeId) && employeeId != Guid.Empty;
    }
}
