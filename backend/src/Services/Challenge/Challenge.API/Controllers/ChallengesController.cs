using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Domain.Entities;
using Challenge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Challenge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/challenges")]
public sealed class ChallengesController(
    IDbContextFactory<ChallengeDbContext> contextFactory,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ChallengesController> logger) : ControllerBase
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
            .Select(x => new ChallengeResponse(x.Id, x.Name, x.Description, x.StartDate, x.EndDate,
                x.RegistrationDeadline, x.RegistrationClosedAtUtc, x.TeamsFinalizedAtUtc, x.CreatedAtUtc))
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
        var challenges = await db.Challenges.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.EndDate >= today &&
                x.RegistrationDeadline >= today && x.RegistrationClosedAtUtc == null &&
                !db.ChallengeParticipations.Any(p => p.ChallengeId == x.Id && p.EmployeeId == employeeId))
            .OrderBy(x => x.StartDate).ThenBy(x => x.Name)
            .Select(x => new ChallengeResponse(x.Id, x.Name, x.Description, x.StartDate, x.EndDate,
                x.RegistrationDeadline, x.RegistrationClosedAtUtc, x.TeamsFinalizedAtUtc, x.CreatedAtUtc))
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
            orderby participation.RespondedAtUtc descending
            select new MyChallengeResponse(challenge.Id, challenge.Name, challenge.Description,
                challenge.StartDate, challenge.EndDate, challenge.RegistrationDeadline,
                participation.ResponseStatus, participation.JoinedAtUtc,
                challenge.TeamsFinalizedAtUtc.HasValue
                    ? (from member in db.ChallengeTeamMembers.AsNoTracking()
                       join team in db.ChallengeTeams.AsNoTracking() on member.TeamId equals team.Id
                       where member.ChallengeId == challenge.Id && member.EmployeeId == employeeId
                       select team.Name).FirstOrDefault()
                    : null)
        ).ToListAsync(cancellationToken);
        return Ok(participations);
    }

    [HttpPost("{challengeId:guid}/join")]
    [Authorize(Roles = "Employee,HrAdmin")]
    public Task<IActionResult> Join(Guid challengeId, CancellationToken cancellationToken) =>
        Respond(challengeId, "Joined", cancellationToken);

    [HttpPost("{challengeId:guid}/response")]
    [Authorize(Roles = "Employee,HrAdmin")]
    public async Task<IActionResult> SetResponse(Guid challengeId, SetChallengeResponseRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.Status is not ("Joined" or "Declined"))
            return BadRequest(new { message = "Response status must be Joined or Declined." });
        return await Respond(challengeId, request.Status, cancellationToken);
    }

    private async Task<IActionResult> Respond(Guid challengeId, string status, CancellationToken cancellationToken)
    {
        if (challengeId == Guid.Empty) return BadRequest(new { message = "Choose a valid challenge." });
        if (!TryGetScope(out var organizationId, out var employeeId)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await IsActiveEmployee(db, employeeId, cancellationToken)) return Forbid();

        var challenge = await db.Challenges.SingleOrDefaultAsync(
            x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound(new { message = "Challenge was not found in your organization." });

        // Registration closure/deadline is the hard boundary: pending invitees cannot join after it.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (challenge.RegistrationClosedAtUtc.HasValue || challenge.RegistrationDeadline < today)
            return Conflict(new { message = "Challenge registration is closed. Pending employees are not participants." });
        if (challenge.EndDate < today)
            return Conflict(new { message = "This challenge has already ended." });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await db.ChallengeParticipations.SingleOrDefaultAsync(
            x => x.ChallengeId == challengeId && x.EmployeeId == employeeId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ResponseStatus == status)
                return Ok(new ChallengeResponseResult(challengeId, status, existing.RespondedAtUtc, true));
            existing.ChangeResponse(status, now);
        }
        else
        {
            if (!await db.ChallengeInvitations.AnyAsync(x => x.ChallengeId == challengeId && x.EmployeeId == employeeId, cancellationToken))
                return Forbid();
            db.ChallengeParticipations.Add(new ChallengeParticipation(Guid.NewGuid(), challengeId, employeeId, now, status));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await using var verify = await contextFactory.CreateDbContextAsync(cancellationToken);
            var concurrent = await verify.ChallengeParticipations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ChallengeId == challengeId && x.EmployeeId == employeeId, cancellationToken);
            if (concurrent is null) throw;
            return Ok(new ChallengeResponseResult(challengeId, concurrent.ResponseStatus, concurrent.RespondedAtUtc, true));
        }
        return Ok(new ChallengeResponseResult(challengeId, status, now, false));
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
            !DateOnly.TryParseExact(request.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate) ||
            !DateOnly.TryParseExact(request.RegistrationDeadline, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var registrationDeadline))
            return BadRequest(new { message = "Enter valid challenge and registration dates." });
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (endDate < startDate || registrationDeadline < today || registrationDeadline > startDate)
            return BadRequest(new { message = "End date must not precede start date, and registration deadline must be today or later and no later than challenge start." });

        var activeEmployees = await GetActiveEmployeesFromIdentity(cancellationToken);
        if (activeEmployees is null) return StatusCode(502, new { message = "Employee directory is unavailable. Challenge was not created; please retry." });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var challenge = new Challenge.Domain.Entities.Challenge(Guid.NewGuid(), organizationId, employeeId,
            request.Name, request.Description, startDate, endDate, now, registrationDeadline);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Challenges.Add(challenge);
        foreach (var employee in activeEmployees)
            db.ChallengeInvitations.Add(new ChallengeInvitation(Guid.NewGuid(), challenge.Id, employee.EmployeeId,
                employee.Email, employee.DisplayName, now));
        await db.SaveChangesAsync(cancellationToken);

        return Created("/api/challenges", new ChallengeResponse(challenge.Id, challenge.Name, challenge.Description,
            challenge.StartDate, challenge.EndDate, challenge.RegistrationDeadline, challenge.RegistrationClosedAtUtc,
            challenge.TeamsFinalizedAtUtc, challenge.CreatedAtUtc));
    }

    [HttpGet("{challengeId:guid}/registration")]
    [Authorize(Roles = "HrAdmin")]
    public async Task<IActionResult> GetRegistration(Guid challengeId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await db.Challenges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound();

        var invitations = await db.ChallengeInvitations.AsNoTracking()
            .Where(x => x.ChallengeId == challengeId).ToListAsync(cancellationToken);
        var responses = await db.ChallengeParticipations.AsNoTracking()
            .Where(x => x.ChallengeId == challengeId).ToListAsync(cancellationToken);
        var joined = responses.Where(x => x.ResponseStatus == "Joined").ToList();
        var teams = await db.ChallengeTeams.AsNoTracking()
            .Where(x => x.ChallengeId == challengeId).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var memberships = await db.ChallengeTeamMembers.AsNoTracking()
            .Where(x => x.ChallengeId == challengeId).ToListAsync(cancellationToken);
        var invitationsByEmployee = invitations.ToDictionary(x => x.EmployeeId);
        var membersByTeam = memberships.GroupBy(x => x.TeamId).ToDictionary(x => x.Key, x => x.ToList());

        var teamResponses = teams.Select(team =>
        {
            var teamMembers = membersByTeam.TryGetValue(team.Id, out var teamMemberships)
                ? teamMemberships.Select(member =>
                {
                    invitationsByEmployee.TryGetValue(member.EmployeeId, out var invite);
                    return new TeamMemberResponse(member.EmployeeId, invite?.DisplayName ?? "Employee", invite?.Email ?? string.Empty);
                }).ToList()
                : new System.Collections.Generic.List<TeamMemberResponse>();
            return new ChallengeTeamResponse(team.Id, team.Name, teamMembers.Count, teamMembers);
        }).ToList();

        var participants = joined.Select(response =>
        {
            invitationsByEmployee.TryGetValue(response.EmployeeId, out var invite);
            var membership = memberships.SingleOrDefault(x => x.EmployeeId == response.EmployeeId);
            var team = membership is null ? null : teams.SingleOrDefault(x => x.Id == membership.TeamId);
            return new RegistrationParticipantResponse(response.EmployeeId, invite?.DisplayName ?? "Employee",
                invite?.Email ?? string.Empty, response.RespondedAtUtc, team?.Id, team?.Name);
        }).ToList();

        return Ok(new RegistrationSummaryResponse(challenge.Id, challenge.Name, challenge.Description,
            challenge.StartDate, challenge.EndDate, challenge.RegistrationDeadline,
            challenge.RegistrationClosedAtUtc, challenge.TeamsFinalizedAtUtc,
            invitations.Count, joined.Count, responses.Count(x => x.ResponseStatus == "Declined"),
            Math.Max(0, invitations.Count - responses.Count), participants, teamResponses));
    }

    [HttpPost("{challengeId:guid}/registration/close")]
    [Authorize(Roles = "HrAdmin")]
    public async Task<IActionResult> CloseRegistration(Guid challengeId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await db.Challenges.SingleOrDefaultAsync(
            x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound();
        challenge.CloseRegistration(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { challenge.Id, challenge.RegistrationClosedAtUtc });
    }

    [HttpPost("{challengeId:guid}/teams")]
    [Authorize(Roles = "HrAdmin")]
    public async Task<IActionResult> CreateTeam(Guid challengeId, CreateChallengeTeamRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        if (request is null || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return BadRequest(new { message = "Team name must contain 1 to 100 characters." });
        if (request.EmployeeIds is null || request.EmployeeIds.Count == 0 ||
            request.EmployeeIds.Any(x => x == Guid.Empty) ||
            request.EmployeeIds.Distinct().Count() != request.EmployeeIds.Count)
            return BadRequest(new { message = "Select one or more valid, unique joined employees for the team." });

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await db.Challenges.SingleOrDefaultAsync(
            x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound();
        if (!challenge.RegistrationClosedAtUtc.HasValue) return Conflict(new { message = "Close registration before creating teams." });
        if (challenge.TeamsFinalizedAtUtc.HasValue) return Conflict(new { message = "Teams have already been finalized." });
        if (await db.ChallengeTeams.AnyAsync(x => x.ChallengeId == challengeId && x.Name == request.Name.Trim(), cancellationToken))
            return Conflict(new { message = "A team with this name already exists in the challenge." });

        var employeeIds = request.EmployeeIds!;
        var joinedIds = await db.ChallengeParticipations
            .Where(x => x.ChallengeId == challengeId && x.ResponseStatus == "Joined")
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        if (employeeIds.Any(x => !joinedIds.Contains(x)))
            return BadRequest(new { message = "Only employees who joined the challenge can be assigned to a team." });
        if (await db.ChallengeTeamMembers.AnyAsync(x => x.ChallengeId == challengeId && employeeIds.Contains(x.EmployeeId), cancellationToken))
            return Conflict(new { message = "An employee is already assigned to a team in this challenge." });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var team = new ChallengeTeam(Guid.NewGuid(), challengeId, request.Name, now);
        db.ChallengeTeams.Add(team);
        foreach (var employeeId in employeeIds)
            db.ChallengeTeamMembers.Add(new ChallengeTeamMember(Guid.NewGuid(), challengeId, team.Id, employeeId, now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "A team with this name or one of its employees is already assigned." }); }
        return Created($"/api/challenges/{challengeId}/teams/{team.Id}", new { team.Id, team.Name, assignedCount = employeeIds.Count });
    }

    [HttpPost("{challengeId:guid}/teams/{teamId:guid}/members")]
    [Authorize(Roles = "HrAdmin")]
    public async Task<IActionResult> AssignTeamMembers(Guid challengeId, Guid teamId, AssignChallengeTeamMembersRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        if (request?.EmployeeIds is null || request.EmployeeIds.Count == 0 || request.EmployeeIds.Any(x => x == Guid.Empty) ||
            request.EmployeeIds.Distinct().Count() != request.EmployeeIds.Count)
            return BadRequest(new { message = "Select one or more valid, unique employees." });
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await db.Challenges.SingleOrDefaultAsync(
            x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound();
        if (!challenge.RegistrationClosedAtUtc.HasValue) return Conflict(new { message = "Close registration before assigning teams." });
        if (challenge.TeamsFinalizedAtUtc.HasValue) return Conflict(new { message = "Teams have already been finalized." });
        if (!await db.ChallengeTeams.AnyAsync(x => x.Id == teamId && x.ChallengeId == challengeId, cancellationToken)) return NotFound();
        var joinedIds = await db.ChallengeParticipations.Where(x => x.ChallengeId == challengeId && x.ResponseStatus == "Joined")
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        if (request.EmployeeIds.Any(x => !joinedIds.Contains(x)))
            return BadRequest(new { message = "Only employees who joined the challenge can be assigned to a team." });
        if (await db.ChallengeTeamMembers.AnyAsync(x => x.ChallengeId == challengeId && request.EmployeeIds.Contains(x.EmployeeId), cancellationToken))
            return Conflict(new { message = "An employee is already assigned to a team in this challenge." });

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var employeeId in request.EmployeeIds)
            db.ChallengeTeamMembers.Add(new ChallengeTeamMember(Guid.NewGuid(), challengeId, teamId, employeeId, now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Conflict(new { message = "One or more employees are already assigned to a team." }); }
        return Ok(new { teamId, assignedCount = request.EmployeeIds.Count });
    }

    [HttpPost("{challengeId:guid}/teams/finalize")]
    [Authorize(Roles = "HrAdmin")]
    public async Task<IActionResult> FinalizeTeams(Guid challengeId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var organizationId, out _)) return Unauthorized();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await db.Challenges.SingleOrDefaultAsync(
            x => x.Id == challengeId && x.OrganizationId == organizationId, cancellationToken);
        if (challenge is null) return NotFound();
        if (!challenge.RegistrationClosedAtUtc.HasValue) return Conflict(new { message = "Close registration before finalizing teams." });
        var joinedCount = await db.ChallengeParticipations.CountAsync(
            x => x.ChallengeId == challengeId && x.ResponseStatus == "Joined", cancellationToken);
        var teamCount = await db.ChallengeTeams.CountAsync(x => x.ChallengeId == challengeId, cancellationToken);
        var assignedCount = await db.ChallengeTeamMembers.CountAsync(x => x.ChallengeId == challengeId, cancellationToken);
        if (joinedCount == 0 || teamCount == 0 || assignedCount != joinedCount)
            return Conflict(new { message = "Create at least one team and assign every joined participant exactly once before finalizing." });
        challenge.FinalizeTeams(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { challenge.Id, challenge.TeamsFinalizedAtUtc, joinedCount, teamCount });
    }

    private async Task<ActiveEmployee[]> GetActiveEmployeesFromIdentity(CancellationToken cancellationToken)
    {
        var identityBaseUrl = configuration["Identity:BaseUrl"] ?? "https://localhost:61329";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{identityBaseUrl.TrimEnd('/')}/api/identity/employees/active");
        var authorization = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authorization)) return null;
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity employee directory returned {StatusCode} during challenge invitation.", response.StatusCode);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ActiveEmployee[]>(cancellationToken: cancellationToken) ?? Array.Empty<ActiveEmployee>();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Identity employee directory is unavailable during challenge invitation.");
            return null;
        }
    }

    private static async Task<bool> IsActiveEmployee(ChallengeDbContext db, Guid employeeId, CancellationToken cancellationToken) =>
        await db.EmployeeStatusProjections.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.IsActive, cancellationToken);

    private bool TryGetScope(out string organizationId, out Guid employeeId)
    {
        organizationId = User.FindFirstValue("organization_id") ?? string.Empty;
        employeeId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(organizationId) &&
            Guid.TryParse(User.FindFirstValue("sub"), out employeeId) && employeeId != Guid.Empty;
    }

    public sealed record ActiveEmployee(Guid EmployeeId, string Email, string DisplayName, string OrganizationId);
    public sealed record CreateChallengeRequest(string Name, string? Description, string StartDate, string EndDate, string RegistrationDeadline);
    public sealed record ChallengeResponse(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate,
        DateOnly RegistrationDeadline, DateTime? RegistrationClosedAtUtc, DateTime? TeamsFinalizedAtUtc, DateTime CreatedAtUtc);
    public sealed record MyChallengeResponse(Guid Id, string Name, string? Description, DateOnly StartDate, DateOnly EndDate,
        DateOnly RegistrationDeadline, string ResponseStatus, DateTime? JoinedAtUtc, string? TeamName);
    public sealed record SetChallengeResponseRequest(string Status);
    public sealed record ChallengeResponseResult(Guid ChallengeId, string Status, DateTime RespondedAtUtc, bool AlreadyResponded);
    public sealed record CreateChallengeTeamRequest(string Name, System.Collections.Generic.List<Guid>? EmployeeIds = null);
    public sealed record AssignChallengeTeamMembersRequest(System.Collections.Generic.List<Guid> EmployeeIds);
    public sealed record TeamMemberResponse(Guid EmployeeId, string DisplayName, string Email);
    public sealed record ChallengeTeamResponse(Guid Id, string Name, int MemberCount, System.Collections.Generic.List<TeamMemberResponse> Members);
    public sealed record RegistrationParticipantResponse(Guid EmployeeId, string DisplayName, string Email, DateTime RespondedAtUtc, Guid? TeamId, string? TeamName);
    public sealed record RegistrationSummaryResponse(Guid ChallengeId, string ChallengeName, string? Description,
        DateOnly StartDate, DateOnly EndDate, DateOnly RegistrationDeadline, DateTime? RegistrationClosedAtUtc,
        DateTime? TeamsFinalizedAtUtc, int InvitedCount, int JoinedCount, int DeclinedCount, int PendingCount,
        System.Collections.Generic.List<RegistrationParticipantResponse> Participants,
        System.Collections.Generic.List<ChallengeTeamResponse> Teams);
}
