using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Domain.Entities;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MySql;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class ChallengeActivitySubmissionApiIntegrationTests : IAsyncLifetime
{
    private const string JwtSigningKey = "event-tracking-integration-test-signing-key-32-bytes-minimum";
    private readonly MySqlContainer database = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithDatabase("teampulse_event_tracking")
        .WithUsername("teampulse")
        .WithPassword("local_event_tracking_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task EmployeeCanLogProgress_Idempotently_AndCompletionBlocksFurtherActivity()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var challengeId = Guid.NewGuid();
        var goalHandler = new StubChallengeApiHandler(JsonSerializer.Serialize(new
        {
            challengeId,
            goalInstructions = "Walk and record your steps.",
            goalTarget = 6000m,
            goalUnit = "steps",
            startDate = today.ToString("yyyy-MM-dd"),
            endDate = today.AddDays(7).ToString("yyyy-MM-dd")
        }));
        using var factory = new EventTrackingApiFactory(database.GetConnectionString(),
            challengeApiHandlerFactory: () => goalHandler);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(employeeId));

        using (var scope = factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(employeeId, true, 1, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var key = Guid.NewGuid();
        var firstPayload = new
        {
            challengeId,
            activityDate = today.ToString("yyyy-MM-dd"),
            quantity = 5000m,
            idempotencyKey = key
        };
        using var first = await client.PostAsJsonAsync("/api/challenge-activities", firstPayload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var identicalRetry = await client.PostAsJsonAsync("/api/challenge-activities", firstPayload);
        Assert.Equal(HttpStatusCode.OK, identicalRetry.StatusCode);

        using var conflictingRetry = await client.PostAsJsonAsync("/api/challenge-activities", new
        {
            challengeId,
            activityDate = today.ToString("yyyy-MM-dd"),
            quantity = 4000m,
            idempotencyKey = key
        });
        Assert.Equal(HttpStatusCode.Conflict, conflictingRetry.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/challenge-activities", new
        {
            challengeId,
            activityDate = today.ToString("yyyy-MM-dd"),
            quantity = 1000m,
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var afterComplete = await client.PostAsJsonAsync("/api/challenge-activities", new
        {
            challengeId,
            activityDate = today.ToString("yyyy-MM-dd"),
            quantity = 1m,
            idempotencyKey = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Conflict, afterComplete.StatusCode);
        Assert.Contains("already completed", await afterComplete.Content.ReadAsStringAsync());

        using var progressResponse = await client.GetAsync($"/api/challenge-activities/mine?challengeId={challengeId}");
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        using var progress = JsonDocument.Parse(await progressResponse.Content.ReadAsStringAsync());
        Assert.Equal(6000m, progress.RootElement.GetProperty("totalQuantity").GetDecimal());
        Assert.Equal(2, progress.RootElement.GetProperty("entries").GetArrayLength());
        Assert.True(goalHandler.RequestCount >= 3);
        Assert.Equal("Bearer", goalHandler.ForwardedAuthorizationScheme);
    }

    [Fact]
    public async Task ProgressRead_WhenEmployeeStatusProjectionIsMissing_ReturnsServiceUnavailable()
    {
        var challengeId = Guid.NewGuid();
        using var factory = new EventTrackingApiFactory(database.GetConnectionString(),
            challengeApiHandlerFactory: () => new StubChallengeApiHandler("{}"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.GetAsync($"/api/challenge-activities/mine?challengeId={challengeId}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ProgressRead_WhenEmployeeIsInactive_ReturnsForbidden()
    {
        var employeeId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        using var factory = new EventTrackingApiFactory(database.GetConnectionString(),
            challengeApiHandlerFactory: () => new StubChallengeApiHandler("{}"));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(employeeId));

        using (var scope = factory.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            db.EmployeeStatusProjections.Add(new EmployeeStatusProjection(employeeId, false, 1, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/challenge-activities/mine?challengeId={challengeId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string CreateToken(Guid employeeId)
    {
        var token = new JwtSecurityToken(
            issuer: "TeamPulse.Identity",
            audience: "TeamPulse.Services",
            claims: new[] { new Claim("sub", employeeId.ToString()), new Claim("role", "Employee") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubChallengeApiHandler(string responseContent) : HttpMessageHandler
    {
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);
        public string? ForwardedAuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            ForwardedAuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });
        }
    }
}
