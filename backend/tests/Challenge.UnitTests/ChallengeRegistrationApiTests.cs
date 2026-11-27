using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Challenge.Domain.Entities;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Challenge.UnitTests;

public sealed class ChallengeRegistrationApiTests(ChallengeMySqlFixture fixture) : IClassFixture<ChallengeMySqlFixture>
{
    [Fact]
    public async Task EmployeeAcknowledgement_RegistrationCloseExcludesPendingEmployees()
    {
        using var factory = fixture.CreateFactory();
        using var joinedClient = factory.CreateClient(); // Starts the API host and applies pending migrations.
        var organizationId = "registration-" + Guid.NewGuid().ToString("N");
        var joinedEmployeeId = Guid.NewGuid();
        var pendingEmployeeId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        await using (var db = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            var challenge = new Challenge.Domain.Entities.Challenge(challengeId, organizationId, Guid.NewGuid(),
                "Acknowledgement test", null, today.AddDays(3), today.AddDays(8), now, today.AddDays(2));
            db.Challenges.Add(challenge);
            db.ChallengeInvitations.Add(new ChallengeInvitation(Guid.NewGuid(), challengeId, joinedEmployeeId,
                "joined@example.test", "Joined Employee", now));
            db.ChallengeInvitations.Add(new ChallengeInvitation(Guid.NewGuid(), challengeId, pendingEmployeeId,
                "pending@example.test", "Pending Employee", now));
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(joinedEmployeeId, true, 1, now));
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(pendingEmployeeId, true, 1, now));
            await db.SaveChangesAsync();
        }

        joinedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(joinedEmployeeId, organizationId, "Employee"));
        using var joined = await joinedClient.PostAsJsonAsync($"/api/challenges/{challengeId}/response", new { status = "Joined" });
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);

        using var hrClient = factory.CreateClient();
        hrClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(Guid.NewGuid(), organizationId, "HrAdmin"));
        using var closed = await hrClient.PostAsync($"/api/challenges/{challengeId}/registration/close", null);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        using var pendingClient = factory.CreateClient();
        pendingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(pendingEmployeeId, organizationId, "Employee"));
        using var pending = await pendingClient.PostAsJsonAsync($"/api/challenges/{challengeId}/response", new { status = "Joined" });
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);

        await using var verify = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync();
        Assert.False(await verify.ChallengeParticipations.AnyAsync(x => x.ChallengeId == challengeId && x.EmployeeId == pendingEmployeeId));
    }

    [Fact]
    public async Task HrCanCreateMultipleTeamsAndFinalizeWhenEveryJoinedEmployeeIsAssigned()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient(); // Starts the API host and applies pending migrations.
        var organizationId = "teams-" + Guid.NewGuid().ToString("N");
        var employeeIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var challengeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        await using (var db = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            var challenge = new Challenge.Domain.Entities.Challenge(challengeId, organizationId, Guid.NewGuid(),
                "Team formation test", null, today, today.AddDays(5), now, today);
            challenge.CloseRegistration(now);
            db.Challenges.Add(challenge);
            foreach (var employeeId in employeeIds)
            {
                db.ChallengeInvitations.Add(new ChallengeInvitation(Guid.NewGuid(), challengeId, employeeId,
                    $"{employeeId:N}@example.test", "Participant", now));
                db.ChallengeParticipations.Add(new ChallengeParticipation(Guid.NewGuid(), challengeId, employeeId, now, "Joined"));
            }
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(Guid.NewGuid(), organizationId, "HrAdmin"));

        using var created = await client.PostAsJsonAsync($"/api/challenges/{challengeId}/teams", new { name = "Team Alpha", employeeIds });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var team = await created.Content.ReadFromJsonAsync<TeamCreatedResponse>();
        Assert.NotNull(team);

        using var finalized = await client.PostAsync($"/api/challenges/{challengeId}/teams/finalize", null);
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);

        await using var verify = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync();
        var challengeAfter = await verify.Challenges.SingleAsync(x => x.Id == challengeId);
        Assert.NotNull(challengeAfter.TeamsFinalizedAtUtc);
        Assert.Equal(2, await verify.ChallengeTeamMembers.CountAsync(x => x.ChallengeId == challengeId));
    }

    [Fact]
    public async Task JoinedEmployeeCanReadChallengeGoal_ButNonParticipantCannot()
    {
        using var factory = fixture.CreateFactory();
        using var client = factory.CreateClient();
        var organizationId = "goal-" + Guid.NewGuid().ToString("N");
        var challengeId = Guid.NewGuid();
        var joinedEmployeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        await using (var db = await factory.Services.GetRequiredService<IDbContextFactory<ChallengeDbContext>>().CreateDbContextAsync())
        {
            db.Challenges.Add(new Challenge.Domain.Entities.Challenge(challengeId, organizationId, Guid.NewGuid(),
                "Goal access test", null, today, today.AddDays(5), now, today,
                "Walk and log your daily steps.", 35000m, "steps"));
            db.ChallengeParticipations.Add(new ChallengeParticipation(Guid.NewGuid(), challengeId, joinedEmployeeId, now, "Joined"));
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(joinedEmployeeId, true, 1, now));
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(otherEmployeeId, true, 1, now));
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(joinedEmployeeId, organizationId, "Employee"));
        using var allowed = await client.GetAsync($"/api/challenges/{challengeId}/activity-goal");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var goal = await allowed.Content.ReadFromJsonAsync<GoalResponse>();
        Assert.NotNull(goal);
        Assert.Equal(35000m, goal.GoalTarget);
        Assert.Equal("steps", goal.GoalUnit);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", ChallengeApiFactory.CreateToken(otherEmployeeId, organizationId, "Employee"));
        using var denied = await client.GetAsync($"/api/challenges/{challengeId}/activity-goal");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private sealed record GoalResponse(Guid ChallengeId, string GoalInstructions, decimal GoalTarget, string GoalUnit, DateOnly StartDate, DateOnly EndDate);\n    private sealed record TeamCreatedResponse(Guid Id, string Name);
}