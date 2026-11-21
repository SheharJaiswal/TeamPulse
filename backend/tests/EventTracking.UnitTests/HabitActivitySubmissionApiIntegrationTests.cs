using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MySql;
using Xunit;

namespace EventTracking.UnitTests;

public sealed class HabitActivitySubmissionApiIntegrationTests : IAsyncLifetime
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
    public async Task Create_WithoutBearerToken_ReturnsUnauthorized_WithoutCallingHabitApi()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, habitApi.RequestCount);
    }

    [Fact]
    public async Task Create_WhenHabitIsNotOwnedByEmployee_ReturnsNotFound_AndDoesNotPersistSubmission()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.Unauthorized);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(employeeId));

        using var response = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, habitApi.RequestCount);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        var body = await history.Content.ReadAsStringAsync();
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task Create_WhenHabitApiIsUnavailable_ReturnsServiceUnavailable_AndDoesNotPersistSubmission()
    {
        var habitApi = new StubHabitApiHandler(throwNetworkError: true);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, habitApi.RequestCount);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        var body = await history.Content.ReadAsStringAsync();
        Assert.Equal("[]", body);
    }

    private EventTrackingApiFactory CreateFactory(StubHabitApiHandler handler) =>
        new(database.GetConnectionString(), habitApiHandlerFactory: () => handler);

    private static object NewSubmission() => new
    {
        habitId = Guid.NewGuid(),
        activityDate = "2026-11-21",
        quantity = 1,
        idempotencyKey = Guid.NewGuid()
    };

    private static string CreateToken(Guid employeeId)
    {
        var token = new JwtSecurityToken(
            issuer: "TeamPulse.Identity",
            audience: "TeamPulse.Services",
            claims: new[] { new Claim("sub", employeeId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubHabitApiHandler(HttpStatusCode statusCode = HttpStatusCode.OK, bool throwNetworkError = false)
        : HttpMessageHandler
    {
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            if (throwNetworkError) throw new HttpRequestException("Simulated Habit API outage.");
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
