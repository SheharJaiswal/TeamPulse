using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
[Authorize(Roles = "Employee,HrAdmin")]
[Route("api/challenge-activities")]
public sealed class ChallengeActivitiesController(
    IChallengeActivitySubmissionStore submissions,
    IEmployeeStatusProjectionReader employeeStatuses,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateChallengeActivityRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        if (request is null || request.ChallengeId == Guid.Empty || request.IdempotencyKey == Guid.Empty ||
            request.Quantity <= 0 || request.Quantity > 1000000m || decimal.Round(request.Quantity, 2) != request.Quantity)
            return BadRequest(new { message = "Choose a challenge, idempotency key and quantity greater than zero up to 1,000,000 (max two decimal places)." });

        var employeeStatus = await employeeStatuses.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeStatus is not { IsActive: true }) return Unauthorized();

        var existing = await submissions.GetByIdempotencyKeyAsync(employeeId, request.IdempotencyKey, cancellationToken);
        if (existing is not null) return ExistingResult(existing, request);

        ChallengeActivityGoalResponse? goal;
        try { goal = await GetChallengeGoalAsync(request.ChallengeId, cancellationToken); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Challenge service is temporarily unavailable." }); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return StatusCode(503, new { message = "Challenge service timed out. Please retry." }); }

        if (goal is null) return NotFound(new { message = "Challenge was not found or is not available to you." });
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (today < goal.StartDate || today > goal.EndDate)
            return Conflict(new { message = "Challenge activity can only be submitted during the challenge dates." });
        if (request.ActivityDate < goal.StartDate || request.ActivityDate > goal.EndDate || request.ActivityDate > today)
            return BadRequest(new { message = "Activity date must be within the challenge dates and cannot be in the future." });

        var current = await submissions.GetMineAsync(employeeId, request.ChallengeId, cancellationToken);
        var total = current.Sum(x => x.Quantity);
        if (total >= goal.GoalTarget)
            return Conflict(new { message = "You have already completed this challenge goal." });

        var created = new ChallengeActivitySubmission(Guid.NewGuid(), employeeId, request.ChallengeId,
            request.ActivityDate, request.Quantity, request.IdempotencyKey, timeProvider.GetUtcNow().UtcDateTime);
        var saved = await submissions.AddAsync(created, cancellationToken);
        if (saved.Id != created.Id) return ExistingResult(saved, request);
        return CreatedAtAction(nameof(GetMine), new { challengeId = saved.ChallengeId }, ToResponse(saved));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] Guid challengeId, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId)) return Unauthorized();
        if (challengeId == Guid.Empty) return BadRequest(new { message = "Choose a valid challenge." });
        var employeeStatus = await employeeStatuses.GetByEmployeeIdAsync(employeeId, cancellationToken);
        if (employeeStatus is not { IsActive: true }) return Unauthorized();

        ChallengeActivityGoalResponse? goal;
        try { goal = await GetChallengeGoalAsync(challengeId, cancellationToken); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Challenge service is temporarily unavailable." }); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return StatusCode(503, new { message = "Challenge service timed out. Please retry." }); }
        if (goal is null) return NotFound();

        var entries = await submissions.GetMineAsync(employeeId, challengeId, cancellationToken);
        var responses = entries.Select(ToResponse).ToList();
        return Ok(new ChallengeActivityProgressResponse(challengeId, entries.Sum(x => x.Quantity), responses));
    }

    private IActionResult ExistingResult(ChallengeActivitySubmission existing, CreateChallengeActivityRequest request)
    {
        if (existing.ChallengeId != request.ChallengeId || existing.ActivityDate != request.ActivityDate || existing.Quantity != request.Quantity)
            return Conflict(new { message = "This idempotency key was already used for a different challenge activity." });
        return Ok(ToResponse(existing));
    }

    private async Task<ChallengeActivityGoalResponse?> GetChallengeGoalAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var bearer) ||
            !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(bearer.Parameter))
            throw new UnauthorizedAccessException();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/challenges/{challengeId}/activity-goal");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Parameter);
        using var response = await httpClientFactory.CreateClient("ChallengeApi").SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException();
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Challenge service validation failed.");
        return await response.Content.ReadFromJsonAsync<ChallengeActivityGoalResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
    }

    private bool TryGetEmployeeId(out Guid employeeId)
    {
        var subject = User.FindFirstValue("sub");
        return Guid.TryParse(subject, out employeeId) && employeeId != Guid.Empty;
    }

    private static ChallengeActivityResponse ToResponse(ChallengeActivitySubmission value) =>
        new(value.Id, value.ChallengeId, value.ActivityDate, value.Quantity, value.SubmittedAtUtc);
}
