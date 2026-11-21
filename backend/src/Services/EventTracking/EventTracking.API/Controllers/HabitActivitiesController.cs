using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Application.Contracts;
using EventTracking.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventTracking.API.Controllers;

[ApiController]
[Authorize]
[Route("api/habit-activities")]
public sealed class HabitActivitiesController(
    IHabitActivitySubmissionStore submissions,
    IEmployeeStatusProjectionReader employeeStatuses,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateHabitActivityRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        if (request.HabitId == Guid.Empty || request.IdempotencyKey == Guid.Empty ||
            request.Quantity is < 1 or > 1000)
            return BadRequest(new { message = "Choose a valid habit, idempotency key and quantity between 1 and 1,000." });

        var employeeStatus = await employeeStatuses.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeStatus is { IsActive: false }) return Unauthorized();

        HabitEligibilityResponse? habit;
        try { habit = await GetHabitAsync(request.HabitId, cancellationToken); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Habit service is temporarily unavailable." }); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return StatusCode(503, new { message = "Habit service timed out. Please retry." }); }

        if (habit is null) return NotFound();
        if (request.ActivityDate < habit.StartDate || (habit.EndDate.HasValue && request.ActivityDate > habit.EndDate.Value))
            return BadRequest(new { message = "Activity date must be within the habit's configured date range." });

        var existing = await submissions.GetByIdempotencyKeyAsync(employeeId, request.IdempotencyKey, cancellationToken);
        if (existing is not null) return ExistingResult(existing, request);

        var created = new HabitActivitySubmission(Guid.NewGuid(), employeeId, request.HabitId,
            request.ActivityDate, request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow().UtcDateTime);
        var saved = await submissions.AddAsync(created, cancellationToken);
        if (saved.Id != created.Id) return ExistingResult(saved, request);
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, ToResponse(saved));
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] Guid? habitId, [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return BadRequest(new { message = "The from date cannot be after the to date." });
        var employeeStatus = await employeeStatuses.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeStatus is { IsActive: false }) return Unauthorized();
        var results = await submissions.GetMineAsync(employeeId, habitId, fromDate, toDate, cancellationToken);
        return Ok(results.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        var employeeStatus = await employeeStatuses.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeStatus is { IsActive: false }) return Unauthorized();
        var result = await submissions.GetAsync(employeeId, id, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    private IActionResult ExistingResult(HabitActivitySubmission existing, CreateHabitActivityRequest request)
    {
        if (existing.HabitId != request.HabitId || existing.ActivityDate != request.ActivityDate || existing.Quantity != request.Quantity)
            return Conflict(new { message = "This idempotency key was already used for a different activity." });
        return Ok(ToResponse(existing));
    }

    private async Task<HabitEligibilityResponse?> GetHabitAsync(Guid habitId, CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var bearer) ||
            !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(bearer.Parameter))
            throw new UnauthorizedAccessException();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/habits/{habitId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Parameter);
        using var response = await httpClientFactory.CreateClient("HabitApi").SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Habit service validation failed.");
        return await response.Content.ReadFromJsonAsync<HabitEligibilityResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
    }

    private bool TryGetEmployeeId(out Guid employeeId)
    {
        var subject = User.FindFirstValue("sub");
        return Guid.TryParse(subject, out employeeId) && employeeId != Guid.Empty;
    }

    private static HabitActivityResponse ToResponse(HabitActivitySubmission value) =>
        new(value.Id, value.HabitId, value.ActivityDate, value.Quantity, value.SubmittedAtUtc);
}
