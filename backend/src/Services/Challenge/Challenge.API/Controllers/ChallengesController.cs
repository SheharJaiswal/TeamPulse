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
[Authorize]
[Route("api/challenges")]
public sealed class ChallengesController(IDbContextFactory<ChallengeDbContext> contextFactory, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "HrAdmin")]
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

    [HttpGet("available")]
    [Authorize(Roles = "Employee,HrAdmin")]
    public async Task<IActionResult> GetAvailable(CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out var employeeId)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await IsActiveEmployee(db, employeeId, cancellationToken)) return Forbid();

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var joinedChallengeIds = db.ChallengeParticipations.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => x.ChallengeId);
        var challenges = await db.Challenges.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EndDate >= today && !joinedChallengeIds.Contains(x.Id))
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.Name)
            .Select(x => new ChallengeResponse(x.Id, x.Name, x.Description, x.StartDate, x.EndDate, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return Ok(challenges);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Employee,HrAdmin")]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out var employeeId)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await IsActiveEmployee(db, employeeId, cancellationToken)) return Forbid();

        var participations = await (
            from participation in db.ChallengeParticipations.AsNoTracking()
            join challenge in db.Challenges.AsNoTracking() on participation.ChallengeId equals challenge.Id
            where participation.EmployeeId == employeeId && challenge.OrganizationId == organizationId
            orderby participation.JoinedAtUtc descending
            select new MyChallengeResponse(challenge.Id, challenge.Name, challenge.Description,
                challenge.StartDate, challenge.EndDate, participation.JoinedAtUtc)
        ).ToListAsync(cancellationToken);
        return Ok(participations);
    }

    [HttpPost("{challengeId:guid}/join")]
    [Authorize(Roles = "Employee,HrAdmin")]
    public async Task<IActionResult> Join(Guid challengeId, CancellationToken cancellationToken)
    {
        if (challengeId == Guid.Empty) return BadRequest(new { message = "Choose a valid challenge." });
        if (!TryGetScope(out var organizationId, out var employeeId)) return Unauthorized();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await IsActiveEmployee(db, employeeId, cancellationToken)) return Forbid();

        var challenge = await db.Challenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound(new { message = "Challenge was not found in your organization." });

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (challenge.EndDate < today)
            return Conflict(new { message = "This challenge has already ended and is no longer accepting participants." });

        var existing = await db.ChallengeParticipations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ChallengeId == challengeId && x.EmployeeId == employeeId, cancellationToken);
        if (existing is not null)
            return Ok(new JoinChallengeResponse(challengeId, existing.JoinedAtUtc, true));

        var joinedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        db.ChallengeParticipations.Add(new ChallengeParticipation(Guid.NewGuid(), challengeId, employeeId, joinedAtUtc));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await using var verificationDb = await contextFactory.CreateDbContextAsync(cancellationToken);
            var concurrentJoin = await verificationDb.ChallengeParticipations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ChallengeId == challengeId && x.EmployeeId == employeeId, cancellationToken);
            if (concurrentJoin is null) throw;
            return Ok(new JoinChallengeResponse(challengeId, concurrentJoin.JoinedAtUtc, true));
        }

        return Ok(new JoinChallengeResponse(challengeId, joinedAtUtc, false));
    }

    [HttpPost]
    [Authorize(Roles = "HrAdmin")]
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

    private static async Task<bool> IsActiveEmployee(ChallengeDbContext db, Guid employeeId, CancellationToken cancellationToken) =>
        await db.EmployeeStatusProjections.AsNoTracking()
            .AnyAsync(x => x.EmployeeId == employeeId && x.IsActive, cancellationToken);

    private bool TryGetScope(out string organizationId, out Guid employeeId)
    {
        organizationId = User.FindFirstValue("organization_id") ?? string.Empty;
        employeeId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(organizationId) &&
            Guid.TryParse(User.FindFirstValue("sub"), out employeeId) && employeeId != Guid.Empty;
    }

    public sealed record CreateChallengeRequest(string Name, string? Description, string StartDate, string EndDate);
    public sealed record ChallengeResponse(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate, DateTime CreatedAtUtc);
    public sealed record MyChallengeResponse(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate, DateTime JoinedAtUtc);
    public sealed record JoinChallengeResponse(Guid ChallengeId, DateTime JoinedAtUtc, bool AlreadyJoined);
}
