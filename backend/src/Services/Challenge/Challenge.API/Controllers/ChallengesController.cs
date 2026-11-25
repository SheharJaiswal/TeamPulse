using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Domain.Entities;
using Challenge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Challenge.API.Controllers;

[ApiController]
[Authorize(Roles = "HrAdmin")]
[Route("api/challenges")]
public sealed class ChallengesController(IDbContextFactory<ChallengeDbContext> contextFactory, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenges = await db.Challenges.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ChallengeResponse(x.Id, x.Name, x.Description, x.StartDate, x.EndDate, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(challenges);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateChallengeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out var employeeId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            return BadRequest(new { message = "Challenge name must contain 1 to 120 characters." });
        if (request.Description?.Length > 1000)
            return BadRequest(new { message = "Description cannot exceed 1,000 characters." });
        if (!DateOnly.TryParseExact(request.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
            !DateOnly.TryParseExact(request.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            return BadRequest(new { message = "Enter valid start and end dates." });
        if (endDate < startDate) return BadRequest(new { message = "End date cannot be before start date." });

        var challenge = new Challenge.Domain.Entities.Challenge(Guid.NewGuid(), organizationId, employeeId,
            request.Name, request.Description, startDate, endDate, timeProvider.GetUtcNow().UtcDateTime);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Challenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return Created("/api/challenges", new ChallengeResponse(challenge.Id, challenge.Name, challenge.Description, challenge.StartDate, challenge.EndDate, challenge.CreatedAtUtc));
    }

    private bool TryGetScope(out string organizationId, out Guid employeeId)
    {
        organizationId = User.FindFirstValue("organization_id") ?? string.Empty;
        employeeId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(organizationId) &&
            Guid.TryParse(User.FindFirstValue("sub"), out employeeId) && employeeId != Guid.Empty;
    }

    public sealed record CreateChallengeRequest(string Name, string? Description, string StartDate, string EndDate);
    public sealed record ChallengeResponse(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate, DateTime CreatedAtUtc);
}
