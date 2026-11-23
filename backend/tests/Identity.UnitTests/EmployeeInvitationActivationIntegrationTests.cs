using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class EmployeeInvitationActivationIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public EmployeeInvitationActivationIntegrationTests(MySqlIntegrationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Activate_CreatesActiveEmployeeWithEmployeeRoleAndConsumesInvitationAtomically()
    {
        var diagnostics = new ConcurrentQueue<string>();
        using var baseFactory = _fixture.CreateFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(new ActivationLogProvider(diagnostics))));
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var invitation = await CreateInvitationAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/identity/invitations/activate",
            new { token = invitation.Token, password = "Strong-password-2026!" });

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected activation success but received {(int)response.StatusCode} {response.StatusCode}. " +
            $"Activation diagnostics: {string.Join(" || ", diagnostics)}");
        Assert.Contains("account is active", await response.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(
            item => item.NormalizedEmail == invitation.Email.ToUpperInvariant());
        var consumedInvitation = await database.EmployeeInvitations.SingleAsync(
            item => item.Id == invitation.Id);

        Assert.Equal("Employee", employee.Role);
        Assert.True(employee.IsActive);
        Assert.NotEqual("temporary", employee.PasswordHash);
        Assert.Equal("Accepted", consumedInvitation.Status);
        Assert.Null(consumedInvitation.TokenDigest);
        Assert.Null(consumedInvitation.PendingNormalizedEmail);
        Assert.False(await database.InvitationDeliverySecrets.AnyAsync(
            item => item.InvitationId == invitation.Id));

        var login = await client.PostAsJsonAsync(
            "/api/identity/login",
            new { email = invitation.Email, password = "Strong-password-2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Activate_CreatesEmployeeWithoutTeamAssignment()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var invitation = await CreateInvitationAsync(factory, teamId: null);

        var response = await ActivateAsync(client, invitation.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(
            item => item.NormalizedEmail == invitation.Email.ToUpperInvariant());

        Assert.Equal("Employee", employee.Role);
        Assert.Equal("org-test", employee.OrganizationId);
        Assert.Null(employee.TeamId);

        var login = await client.PostAsJsonAsync(
            "/api/identity/login",
            new { email = invitation.Email, password = "Strong-password-2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(
            loginBody.GetProperty("accessToken").GetString()!);
        Assert.DoesNotContain(accessToken.Claims, claim => claim.Type == "team_id");
    }

    [Fact]
    public async Task Activate_ConcurrentRedemptionsCreateExactlyOneEmployee()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var invitation = await CreateInvitationAsync(factory);

        var attempts = await Task.WhenAll(
            ActivateAsync(client, invitation.Token),
            ActivateAsync(client, invitation.Token));

        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.BadRequest);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, await database.Employees.CountAsync(
            item => item.NormalizedEmail == invitation.Email.ToUpperInvariant()));
        var consumed = await database.EmployeeInvitations.SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal("Accepted", consumed.Status);
        Assert.Null(consumed.TokenDigest);
    }

    [Fact]
    public async Task Activate_UsesGenericFailureForInvalidExpiredCancelledAndUsedTokens_AndRateLimits()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);

        var expired = await CreateInvitationAsync(factory);
        var cancelled = await CreateInvitationAsync(factory);
        var used = await CreateInvitationAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_invitations SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-1)} WHERE Id = {expired.Id}");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
            var result = await service.CancelAsync(cancelled.Id, CancellationToken.None);
            Assert.Equal(EmployeeInvitationCommandStatus.Updated, result.Status);
        }

        var firstUse = await ActivateAsync(client, used.Token);
        Assert.Equal(HttpStatusCode.OK, firstUse.StatusCode);

        var failures = new[]
        {
            await ActivateAsync(client, "not-a-valid-token"),
            await ActivateAsync(client, expired.Token),
            await ActivateAsync(client, cancelled.Token),
            await ActivateAsync(client, used.Token)
        };

        Assert.All(failures, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        var bodies = await Task.WhenAll(failures.Select(response => response.Content.ReadAsStringAsync()));
        Assert.All(bodies, body => Assert.Equal(bodies[0], body));

        var limited = await ActivateAsync(client, "another-invalid-token");
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
    }

    [Fact]
    public async Task Activate_RejectsWeakPasswordsWithoutConsumingInvitation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var invitation = await CreateInvitationAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/identity/invitations/activate",
            new { token = invitation.Token, password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var pending = await database.EmployeeInvitations.SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal("Pending", pending.Status);
        Assert.NotNull(pending.TokenDigest);
        Assert.False(await database.Employees.AnyAsync(
            item => item.NormalizedEmail == invitation.Email.ToUpperInvariant()));
    }

    private async Task EnsureDatabaseAsync(HttpClient client)
    {
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(Guid Id, string Email, string Token)> CreateInvitationAsync(
        WebApplicationFactory<Program> factory,
        string? teamId = "team-test")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
        var email = $"activation-{Guid.NewGuid():N}@example.com";
        var result = await service.CreateAsync(
            Guid.NewGuid(),
            new CreateEmployeeInvitationRequest(email, "Activation Test Employee", "org-test", teamId),
            CancellationToken.None);
        Assert.Equal(EmployeeInvitationCommandStatus.Created, result.Status);

        var secret = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .InvitationDeliverySecrets.SingleAsync(item => item.InvitationId == result.Invitation!.InvitationId);
        return (result.Invitation.InvitationId, email, tokenService.Unprotect(secret.ProtectedToken));
    }


    private sealed class ActivationLogProvider(ConcurrentQueue<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ActivationLogger(categoryName, entries);
        public void Dispose() { }

        private sealed class ActivationLogger(string categoryName, ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel) && categoryName.Contains("EmployeeInvitationActivationController",
                        StringComparison.Ordinal))
                    entries.Enqueue(formatter(state, null));
            }
        }
    }

    private static Task<HttpResponseMessage> ActivateAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync(
            "/api/identity/invitations/activate",
            new { token, password = "Strong-password-2026!" });
}
