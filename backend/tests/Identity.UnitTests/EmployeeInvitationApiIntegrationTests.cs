using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.UnitTests;

public sealed class EmployeeInvitationApiIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private const string Issuer = "TeamPulse.Identity.IntegrationTests";
    private const string Audience = "TeamPulse.Services.IntegrationTests";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";
    private readonly MySqlIntegrationFixture _fixture;

    public EmployeeInvitationApiIntegrationTests(MySqlIntegrationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task InvitationEndpoints_RequireHrAdminAndSupportCreateInspectResendAndCancel()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);

        var anonymous = await client.PostAsJsonAsync("/api/identity/invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "Employee"));
        var employee = await client.PostAsJsonAsync("/api/identity/invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "OrganizationAdmin"));
        var organizationAdmin = await client.PostAsJsonAsync("/api/identity/invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, organizationAdmin.StatusCode);

        var hrAdminId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(hrAdminId, "HrAdmin"));

        var createdResponse = await client.PostAsJsonAsync(
            "/api/identity/invitations", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invitationId = created.GetProperty("invitationId").GetGuid();
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.Equal(1, created.GetProperty("deliveryGeneration").GetInt32());

        var getResponse = await client.GetAsync($"/api/identity/invitations/{invitationId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var resendResponse = await client.PostAsync(
            $"/api/identity/invitations/{invitationId}/resend", content: null);
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var resent = await resendResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, resent.GetProperty("deliveryGeneration").GetInt32());

        var cancelResponse = await client.PostAsync($"/api/identity/invitations/{invitationId}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        var resendTerminal = await client.PostAsync(
            $"/api/identity/invitations/{invitationId}/resend", content: null);
        Assert.Equal(HttpStatusCode.Conflict, resendTerminal.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var invitation = await database.EmployeeInvitations.SingleAsync(item => item.Id == invitationId);
        Assert.Equal(hrAdminId, invitation.InvitedByEmployeeId);
        Assert.Equal("Cancelled", invitation.Status);
        Assert.Null(invitation.TokenDigest);
        Assert.False(await database.InvitationDeliverySecrets.AnyAsync(
            secret => secret.InvitationId == invitationId));
    }

    [Fact]
    public async Task CreateInvitation_RejectsInvalidFieldsBeforeWritingAnything()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        int invitationsBefore;
        int secretsBefore;
        int deliveryEventsBefore;
        await using (var beforeScope = factory.Services.CreateAsyncScope())
        {
            var beforeDatabase = beforeScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            invitationsBefore = await beforeDatabase.EmployeeInvitations.CountAsync();
            secretsBefore = await beforeDatabase.InvitationDeliverySecrets.CountAsync();
            deliveryEventsBefore = await beforeDatabase.OutboxMessages.CountAsync(
                item => item.EventType == "EmployeeInvitationDeliveryRequested");
        }

        var invalidRequests = new[]
        {
            new { email = "not-an-email", displayName = "Employee", organizationId = "org", teamId = "team" },
            new { email = "employee@example.com", displayName = " ", organizationId = "org", teamId = "team" },
            new { email = "employee@example.com", displayName = new string('x', 151), organizationId = "org", teamId = "team" },
            new { email = "employee@example.com", displayName = "Employee", organizationId = " ", teamId = "team" },
            new { email = "employee@example.com", displayName = "Employee", organizationId = "org", teamId = new string('t', 101) }
        };

        foreach (var request in invalidRequests)
        {
            var response = await client.PostAsJsonAsync("/api/identity/invitations", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(invitationsBefore, await database.EmployeeInvitations.CountAsync());
        Assert.Equal(secretsBefore, await database.InvitationDeliverySecrets.CountAsync());
        Assert.Equal(deliveryEventsBefore, await database.OutboxMessages.CountAsync(
            item => item.EventType == "EmployeeInvitationDeliveryRequested"));
    }

    [Fact]
    public async Task InvitationEndpoints_ReturnNotFoundForUnknownInvitation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/identity/invitations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/identity/invitations/{id}/resend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/identity/invitations/{id}/cancel", null)).StatusCode);
    }

    private async Task EnsureDatabaseAsync(HttpClient client)
    {
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static object ValidRequest() => new
    {
        email = $"invited-{Guid.NewGuid():N}@example.com",
        displayName = "New Employee",
        organizationId = "org-test",
        teamId = "team-test"
    };

    private static string CreateToken(Guid subject, string role)
    {
        var claims = new[]
        {
            new Claim("sub", subject.ToString()),
            new Claim("role", role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
