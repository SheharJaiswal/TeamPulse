using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class IdentityApiIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private const string Password = "Strong-Integration-Password-123!";
    private const string InitialMigration = "20261120030000_InitialIdentitySchema";
    private readonly MySqlIntegrationFixture _fixture;

    public IdentityApiIntegrationTests(MySqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Registration_PersistsHash_AndRejectsDuplicateNormalizedEmail()
    {
        var email = $"duplicate-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        var firstResponse = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var auth = await firstResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth.TokenType);

        var duplicateResponse = await RegisterAsync(client, $"  {email.ToUpperInvariant()}  ");
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(
            x => x.NormalizedEmail == email.ToUpperInvariant());

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Employee>();
        Assert.NotEqual(Password, employee.PasswordHash);
        Assert.Equal(
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(employee, employee.PasswordHash, Password));
    }

    [Fact]
    public async Task Database_EnforcesUniqueNormalizedEmailIndex()
    {
        var email = $"unique-index-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        var registration = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        database.Employees.Add(new Employee(
            Guid.NewGuid(),
            $"different-{Guid.NewGuid():N}@teampulse.test",
            email.ToUpperInvariant(),
            "Duplicate Normalized Email",
            "test-hash",
            "org-integration",
            "team-integration",
            "Employee"));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task Me_RequiresBearerToken_AndReturnsValidatedIdentityClaims()
    {
        var email = $"claims-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        var anonymousResponse = await client.GetAsync("/api/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var registration = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.GetAsync("/api/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var me = document.RootElement;

        Assert.Equal(auth.Employee.EmployeeId.ToString(), me.GetProperty("employeeId").GetString());
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Integration Test Employee", me.GetProperty("displayName").GetString());
        Assert.Equal("org-integration", me.GetProperty("organizationId").GetString());
        Assert.Equal("team-integration", me.GetProperty("teamId").GetString());
        Assert.Equal("Employee", me.GetProperty("role").GetString());
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        return client.PostAsJsonAsync("/api/identity/register", new RegisterRequest(
            email,
            Password,
            "Integration Test Employee",
            "org-integration",
            "team-integration"));
    }
}
