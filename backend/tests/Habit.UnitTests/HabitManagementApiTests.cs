using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Habit.UnitTests;

public sealed class HabitManagementApiTests(HabitMySqlFixture fixture) : IClassFixture<HabitMySqlFixture>
{
    [Fact]
    public async Task HabitEndpoints_RequireAuthentication_AndEnforceOwnerIsolation()
    {
        using var factory = fixture.CreateFactory();
        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var ownerId = Guid.NewGuid();
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(ownerId));

        var createResponse = await ownerClient.PostAsJsonAsync("/api/habits", new
        {
            name = "Drink water",
            description = "Stay hydrated",
            category = "Wellness",
            frequency = 1,
            targetCount = 8,
            startDate = "2026-11-21",
            endDate = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var habitId = createdJson.RootElement.GetProperty("id").GetGuid();
        Assert.False(createdJson.RootElement.TryGetProperty("ownerEmployeeId", out _));

        var ownRead = await ownerClient.GetAsync($"/api/habits/{habitId}");
        Assert.Equal(HttpStatusCode.OK, ownRead.StatusCode);
        var ownList = await ownerClient.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.OK, ownList.StatusCode);
        using var listJson = JsonDocument.Parse(await ownList.Content.ReadAsStringAsync());
        Assert.Equal(1, listJson.RootElement.GetArrayLength());

        var updateResponse = await ownerClient.PutAsJsonAsync($"/api/habits/{habitId}", new
        {
            name = "Drink water consistently",
            description = "Stay hydrated throughout the day",
            category = "Wellness",
            frequency = 1,
            targetCount = 10,
            startDate = "2026-11-21",
            endDate = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var archiveResponse = await ownerClient.PostAsync($"/api/habits/{habitId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);
        var repeatedArchive = await ownerClient.PostAsync($"/api/habits/{habitId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, repeatedArchive.StatusCode);

        var updateArchived = await ownerClient.PutAsJsonAsync($"/api/habits/{habitId}", new
        {
            name = "Should not update",
            description = (string?)null,
            category = (string?)null,
            frequency = 1,
            targetCount = 1,
            startDate = "2026-11-21",
            endDate = (string?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, updateArchived.StatusCode);

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));
        var otherRead = await otherClient.GetAsync($"/api/habits/{habitId}");
        Assert.Equal(HttpStatusCode.NotFound, otherRead.StatusCode);
        var otherList = await otherClient.GetAsync("/api/habits");
        Assert.Equal(HttpStatusCode.OK, otherList.StatusCode);
        using var otherListJson = JsonDocument.Parse(await otherList.Content.ReadAsStringAsync());
        Assert.Equal(0, otherListJson.RootElement.GetArrayLength());
    }

    private static string CreateToken(Guid employeeId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(HabitApiFactory.JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: HabitApiFactory.JwtIssuer,
            audience: HabitApiFactory.JwtAudience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, employeeId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
