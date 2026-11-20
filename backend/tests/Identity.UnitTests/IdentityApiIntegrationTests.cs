using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
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

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public async Task Registration_IsUnavailableOutsideDevelopment(string environmentName)
    {
        var email = $"disabled-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory(environmentName);
        using var client = factory.CreateClient();

        var response = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await database.Employees.AnyAsync(
            x => x.NormalizedEmail == email.ToUpperInvariant()));
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

    [Fact]
    public async Task Login_WhenEmployeeIsInactive_ReturnsGenericUnauthorizedWithoutChangingHash()
    {
        var email = $"inactive-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        var hasher = new PasswordHasher<Employee>();
        var employee = new Employee(
            Guid.NewGuid(), email, email.ToUpperInvariant(), "Inactive Employee",
            "placeholder", "org-integration", "team-integration", "Employee");
        var originalHash = hasher.HashPassword(employee, Password);
        var persistedEmployee = new Employee(
            employee.Id, employee.Email, employee.NormalizedEmail, employee.DisplayName,
            originalHash, employee.OrganizationId, employee.TeamId, employee.Role);

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.Employees.Add(persistedEmployee);
            await database.SaveChangesAsync();

            var updatedRows = await database.Employees
                .Where(x => x.Id == employee.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false));
            Assert.Equal(1, updatedRows);
        }

        var response = await client.PostAsJsonAsync(
            "/api/identity/login", new LoginRequest(email, Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var responseDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Invalid email or password.",
            responseDocument.RootElement.GetProperty("message").GetString());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var storedEmployee = await verifyDatabase.Employees.SingleAsync(x => x.Id == employee.Id);

        Assert.False(storedEmployee.IsActive);
        Assert.Equal(originalHash, storedEmployee.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(storedEmployee, storedEmployee.PasswordHash, Password));
    }

    [Fact]
    public async Task Login_WhenLegacyPasswordHashIsVerified_UpgradesAndPersistsCurrentHashBeforeReturningToken()
    {
        var email = $"legacy-{Guid.NewGuid():N}@teampulse.test";
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();

        var legacyHasher = new PasswordHasher<Employee>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));

        var originalEmployee = new Employee(
            Guid.NewGuid(), email, email.ToUpperInvariant(), "Legacy Employee",
            "placeholder", "org-integration", "team-integration", "Employee");
        var legacyHash = legacyHasher.HashPassword(originalEmployee, Password);
        var legacyEmployee = new Employee(
            originalEmployee.Id, originalEmployee.Email, originalEmployee.NormalizedEmail,
            originalEmployee.DisplayName, legacyHash, originalEmployee.OrganizationId,
            originalEmployee.TeamId, originalEmployee.Role);

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.Employees.Add(legacyEmployee);
            await database.SaveChangesAsync();
        }

        var currentHasher = new PasswordHasher<Employee>();
        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            currentHasher.VerifyHashedPassword(legacyEmployee, legacyHash, Password));

        var loginResponse = await client.PostAsJsonAsync(
            "/api/identity/login", new LoginRequest(email, Password));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var persistedEmployee = await verifyDatabase.Employees.SingleAsync(
            x => x.NormalizedEmail == email.ToUpperInvariant());

        Assert.NotEqual(legacyHash, persistedEmployee.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            currentHasher.VerifyHashedPassword(
                persistedEmployee, persistedEmployee.PasswordHash, Password));
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
