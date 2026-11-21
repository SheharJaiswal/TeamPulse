using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventTracking.Application.Abstractions;
using EventTracking.Domain.Entities;
using EventTracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task InactiveEmployee_CannotCreateOrReadActivitySubmissions()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(employeeId));

        using (var scope = factory.Services.CreateScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EventTrackingDbContext>>();
            await using var databaseContext = await contextFactory.CreateDbContextAsync();
            databaseContext.EmployeeStatusProjections.Add(new EmployeeStatusProjection(
                employeeId, false, 2, DateTime.UtcNow));
            await databaseContext.SaveChangesAsync();

            var store = scope.ServiceProvider.GetRequiredService<IHabitActivitySubmissionStore>();
            await store.AddAsync(new HabitActivitySubmission(submissionId, employeeId, Guid.NewGuid(),
                new DateOnly(2026, 11, 21), 1, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
        }

        using var createResponse = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission());
        using var historyResponse = await client.GetAsync("/api/habit-activities");
        using var detailResponse = await client.GetAsync($"/api/habit-activities/{submissionId}");

        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, historyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detailResponse.StatusCode);
        Assert.Equal(0, habitApi.RequestCount);
    }

    [Fact]
    public async Task Create_WhenHabitIsNotOwnedByEmployee_ReturnsNotFound_AndDoesNotPersistSubmission()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.Unauthorized);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var bearerToken = CreateToken(employeeId);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", bearerToken);

        using var response = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission(habitId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, habitApi.RequestCount);
        Assert.Equal($"/api/habits/{habitId}", habitApi.LastRequestPath);
        Assert.Equal("Bearer", habitApi.ForwardedAuthorizationScheme);
        Assert.Equal(bearerToken, habitApi.ForwardedAuthorizationParameter);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Create_WhenIdempotencyKeyIsReusedWithDifferentPayload_ReturnsConflictWithoutCallingHabitApi()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(employeeId));

        var originalSubmissionId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IHabitActivitySubmissionStore>();
            await store.AddAsync(new HabitActivitySubmission(originalSubmissionId, employeeId, habitId,
                new DateOnly(2026, 11, 21), 1, idempotencyKey, DateTime.UtcNow), CancellationToken.None);
        }

        using var identicalRetry = await client.PostAsJsonAsync("/api/habit-activities", new
        {
            habitId,
            activityDate = "2026-11-21",
            quantity = 1,
            idempotencyKey
        });
        Assert.Equal(HttpStatusCode.OK, identicalRetry.StatusCode);
        using (var retryDocument = JsonDocument.Parse(await identicalRetry.Content.ReadAsStringAsync()))
            Assert.Equal(originalSubmissionId, retryDocument.RootElement.GetProperty("id").GetGuid());

        using var conflictingRetry = await client.PostAsJsonAsync("/api/habit-activities", new
        {
            habitId,
            activityDate = "2026-11-21",
            quantity = 2,
            idempotencyKey
        });

        Assert.Equal(HttpStatusCode.Conflict, conflictingRetry.StatusCode);
        Assert.Contains("already used for a different activity.",
            await conflictingRetry.Content.ReadAsStringAsync());
        Assert.Equal(0, habitApi.RequestCount);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Single(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Create_WhenRequestContainsInvalidIdentifiersOrQuantity_ReturnsBadRequestBeforeCallingHabitApi()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));

        var validHabitId = Guid.NewGuid();
        var validIdempotencyKey = Guid.NewGuid();
        var invalidRequests = new object[]
        {
            new { habitId = validHabitId, activityDate = "2026-11-21", quantity = 0, idempotencyKey = validIdempotencyKey },
            new { habitId = validHabitId, activityDate = "2026-11-21", quantity = 1001, idempotencyKey = validIdempotencyKey },
            new { habitId = Guid.Empty, activityDate = "2026-11-21", quantity = 1, idempotencyKey = validIdempotencyKey },
            new { habitId = validHabitId, activityDate = "2026-11-21", quantity = 1, idempotencyKey = Guid.Empty }
        };

        foreach (var invalidRequest in invalidRequests)
        {
            using var response = await client.PostAsJsonAsync("/api/habit-activities", invalidRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(0, habitApi.RequestCount);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task GetMine_FiltersByHabitAndInclusiveDateRange_AndReturnsOnlyCallerRecords()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var otherHabitId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(employeeId));

        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IHabitActivitySubmissionStore>();
            await store.AddAsync(new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId,
                new DateOnly(2026, 11, 20), 1, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
            await store.AddAsync(new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId,
                new DateOnly(2026, 11, 21), 2, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
            await store.AddAsync(new HabitActivitySubmission(Guid.NewGuid(), employeeId, habitId,
                new DateOnly(2026, 11, 22), 3, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
            await store.AddAsync(new HabitActivitySubmission(Guid.NewGuid(), employeeId, otherHabitId,
                new DateOnly(2026, 11, 21), 4, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
            await store.AddAsync(new HabitActivitySubmission(Guid.NewGuid(), otherEmployeeId, habitId,
                new DateOnly(2026, 11, 21), 5, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
        }

        using var response = await client.GetAsync(
            $"/api/habit-activities?habitId={habitId}&fromDate=2026-11-20&toDate=2026-11-21");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var records = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, records.Length);
        Assert.Contains(records, item => DateOnly.Parse(item.GetProperty("activityDate").GetString()!) == new DateOnly(2026, 11, 20));
        Assert.Contains(records, item => DateOnly.Parse(item.GetProperty("activityDate").GetString()!) == new DateOnly(2026, 11, 21));
        Assert.Equal(0, habitApi.RequestCount);
    }

    [Fact]
    public async Task GetMine_WhenDateRangeIsReversed_ReturnsBadRequest()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.GetAsync(
            "/api/habit-activities?fromDate=2026-11-22&toDate=2026-11-20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("from date cannot be after the to date",
            await response.Content.ReadAsStringAsync());
        Assert.Equal(0, habitApi.RequestCount);
    }

    [Fact]
    public async Task GetMine_WhenDateFilterIsMalformed_ReturnsBadRequest()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.GetAsync(
            "/api/habit-activities?fromDate=not-a-date&toDate=2026-11-22");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, habitApi.RequestCount);
    }

    [Fact]
    public async Task Get_WhenSubmissionBelongsToAnotherEmployee_ReturnsNotFound()
    {
        var habitApi = new StubHabitApiHandler(HttpStatusCode.OK);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        var ownerId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IHabitActivitySubmissionStore>();
            await store.AddAsync(new HabitActivitySubmission(submissionId, ownerId, Guid.NewGuid(),
                new DateOnly(2026, 11, 21), 1, Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);
        }

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(otherEmployeeId));
        using var response = await client.GetAsync($"/api/habit-activities/{submissionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, habitApi.RequestCount);
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
        using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Create_WhenHabitApiTimesOut_ReturnsServiceUnavailable_AndDoesNotPersistSubmission()
    {
        var habitApi = new StubHabitApiHandler(throwTimeoutError: true);
        using var factory = CreateFactory(habitApi);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid()));

        using var response = await client.PostAsJsonAsync("/api/habit-activities", NewSubmission());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, habitApi.RequestCount);

        using var history = await client.GetAsync("/api/habit-activities");
        history.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    private EventTrackingApiFactory CreateFactory(StubHabitApiHandler handler) =>
        new(database.GetConnectionString(), habitApiHandlerFactory: () => handler);

    private static object NewSubmission(Guid? habitId = null) => new
    {
        habitId = habitId ?? Guid.NewGuid(),
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

    private sealed class StubHabitApiHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        bool throwNetworkError = false,
        bool throwTimeoutError = false) : HttpMessageHandler
    {
        private int requestCount;
        public int RequestCount => Volatile.Read(ref requestCount);
        public string? LastRequestPath { get; private set; }
        public string? ForwardedAuthorizationScheme { get; private set; }
        public string? ForwardedAuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            LastRequestPath = request.RequestUri?.PathAndQuery;
            ForwardedAuthorizationScheme = request.Headers.Authorization?.Scheme;
            ForwardedAuthorizationParameter = request.Headers.Authorization?.Parameter;
            if (throwNetworkError) throw new HttpRequestException("Simulated Habit API outage.");
            if (throwTimeoutError) throw new TaskCanceledException("Simulated Habit API timeout.");
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
