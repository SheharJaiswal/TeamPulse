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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.UnitTests;

public sealed class EmployeeLifecycleApiIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private const string Issuer = "TeamPulse.Identity.IntegrationTests";
    private const string Audience = "TeamPulse.Services.IntegrationTests";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";
    private readonly MySqlIntegrationFixture _fixture;

    public EmployeeLifecycleApiIntegrationTests(MySqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LifecycleEndpoints_RequireHrAdminRole()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var targetId = await SeedEmployeeAsync(factory);

        var anonymous = await SendDeactivateAsync(client, targetId, null, "auth-anon");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "Employee"));
        var employee = await SendDeactivateAsync(client, targetId, "auth-employee", "auth-employee");
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "OrganizationAdmin"));
        var organizationAdmin = await SendDeactivateAsync(client, targetId, "auth-org", "auth-org");
        Assert.Equal(HttpStatusCode.Forbidden, organizationAdmin.StatusCode);

        var actorId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(actorId, "HrAdmin"));
        var hrAdmin = await SendDeactivateAsync(client, targetId, "auth-hr", "auth-hr");
        Assert.Equal(HttpStatusCode.OK, hrAdmin.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audit = await database.EmployeeLifecycleAudits.SingleAsync(x => x.EmployeeId == targetId);
        Assert.Equal(actorId, audit.ActorEmployeeId);
        Assert.Equal("hr-admin-api", audit.Source);
    }

    [Fact]
    public async Task LifecycleEndpoints_RejectMissingOrInvalidSubjectClaimBeforeMutation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        var correlationIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var invalidSubjects = new string?[] { null, "not-a-guid" };

        for (var index = 0; index < invalidSubjects.Length; index++)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateTokenWithRawSubject(invalidSubjects[index], "HrAdmin"));

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
            {
                Content = JsonContent.Create(new { reasonCode = "valid-reason" })
            };
            request.Headers.Add("Idempotency-Key", $"invalid-subject-{index}");
            request.Headers.Add("X-Correlation-ID", correlationIds[index].ToString());

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);

        Assert.True(employee.IsActive);
        Assert.Equal(0, employee.LifecycleVersion);
        Assert.False(await database.EmployeeLifecycleAudits.AnyAsync(x => x.EmployeeId == employeeId));
        Assert.False(await database.OutboxMessages.AnyAsync(x => correlationIds.Contains(x.CorrelationId)));
    }

    [Fact]
    public async Task Deactivate_ConcurrentSameKeyRequests_CreateOneTransitionAuditAndOutbox()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var first = SendDeactivateAsync(client, employeeId, "same-key", "same-reason");
        var second = SendDeactivateAsync(client, employeeId, "same-key", "  same-reason  ");
        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var payloads = await Task.WhenAll(responses.Select(ReadJsonAsync));
        Assert.Contains(payloads, payload => payload.RootElement.GetProperty("outcome").GetString() == "transitioned");
        Assert.Contains(payloads, payload => payload.RootElement.GetProperty("outcome").GetString() == "replayed");
        var eventIds = payloads.Select(payload => payload.RootElement.GetProperty("eventId").GetString()).Distinct();
        Assert.Single(eventIds);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);
        var audit = await database.EmployeeLifecycleAudits.SingleAsync(x => x.EmployeeId == employeeId);
        var outbox = await database.OutboxMessages.SingleAsync(x => x.EventId == audit.Id);

        Assert.False(employee.IsActive);
        Assert.Equal(1, employee.LifecycleVersion);
        Assert.Equal(1, audit.LifecycleVersion);
        Assert.Equal("EmployeeDeactivated", outbox.EventType);
        Assert.Equal(1, outbox.SchemaVersion);

        using var envelope = JsonDocument.Parse(outbox.Payload);
        Assert.Equal(audit.Id.ToString(), envelope.RootElement.GetProperty("eventId").GetString());
        Assert.Equal("EmployeeDeactivated", envelope.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(1, envelope.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(audit.ActorEmployeeId.ToString(),
            envelope.RootElement.GetProperty("payload").GetProperty("actorEmployeeId").GetString());

        var conflictingReuse = await SendReactivateAsync(client, employeeId, "different-command", "same-key");
        Assert.Equal(HttpStatusCode.Conflict, conflictingReuse.StatusCode);
        Assert.Equal(1, await database.EmployeeLifecycleAudits.CountAsync(x => x.EmployeeId == employeeId));
        Assert.Equal(1, await database.OutboxMessages.CountAsync(x => x.EventId == audit.Id));
    }

    [Fact]
    public async Task Reactivate_ChangesStateAndSameStateRequestIsNoOp()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory, isActive: false);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var first = await SendReactivateAsync(client, employeeId, "reactivate-first", "reactivate-1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal("transitioned", firstJson.RootElement.GetProperty("outcome").GetString());
        Assert.True(firstJson.RootElement.GetProperty("isActive").GetBoolean());

        var noOp = await SendReactivateAsync(client, employeeId, "reactivate-noop", "reactivate-2");
        Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
        using var noOpJson = JsonDocument.Parse(await noOp.Content.ReadAsStringAsync());
        Assert.Equal("alreadyInDesiredState", noOpJson.RootElement.GetProperty("outcome").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);
        Assert.True(employee.IsActive);
        Assert.Equal(1, employee.LifecycleVersion);
        Assert.Equal(1, await database.EmployeeLifecycleAudits.CountAsync(x => x.EmployeeId == employeeId));
        var eventId = Guid.Parse(firstJson.RootElement.GetProperty("eventId").GetString()!);
        Assert.Equal(1, await database.OutboxMessages.CountAsync(x => x.EventId == eventId));
    }

    [Fact]
    public async Task LifecycleWriteFailure_RollsBackEmployeeAuditAndOutboxTogether()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        var correlationId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySql(_fixture.ConnectionString, ServerVersion.AutoDetect(_fixture.ConnectionString))
            .AddInterceptors(new FailAfterSaveInterceptor())
            .Options;

        await using (var failingDatabase = new IdentityDbContext(options))
        {
            var service = new EmployeeLifecycleService(failingDatabase);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
                employeeId, Guid.NewGuid(), false, "rollback-key", "employment-ended",
                null, correlationId, CancellationToken.None));
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await verifyDatabase.Employees.SingleAsync(x => x.Id == employeeId);
        Assert.True(employee.IsActive);
        Assert.Equal(0, employee.LifecycleVersion);
        Assert.False(await verifyDatabase.EmployeeLifecycleAudits.AnyAsync(x => x.EmployeeId == employeeId));
        Assert.False(await verifyDatabase.OutboxMessages.AnyAsync(x => x.CorrelationId == correlationId));
    }

    [Fact]
    public async Task LifecycleEndpoint_RejectsInvalidHeadersAndRequest()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var missingKey = await client.PostAsJsonAsync(
            $"/api/identity/employees/{employeeId}/deactivate", new { reasonCode = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "valid-key");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "not-a-guid");
        var invalidCorrelation = await client.PostAsJsonAsync(
            $"/api/identity/employees/{employeeId}/deactivate", new { reasonCode = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCorrelation.StatusCode);
    }

    [Fact]
    public async Task LifecycleEndpoint_RejectsInvalidOrRepeatedIdempotencyAndCorrelationHeadersWithoutMutation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var correlationIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var invalidHeaders = new[]
        {
            new[] { ("Idempotency-Key", "   "), ("X-Correlation-ID", correlationIds[0].ToString()) },
            new[] { ("Idempotency-Key", new string('k', 129)), ("X-Correlation-ID", correlationIds[1].ToString()) },
            new[] { ("Idempotency-Key", "first"), ("Idempotency-Key", "second"),
                ("X-Correlation-ID", correlationIds[2].ToString()) },
            new[] { ("Idempotency-Key", "valid-key"), ("X-Correlation-ID", Guid.NewGuid().ToString()),
                ("X-Correlation-ID", Guid.NewGuid().ToString()) }
        };

        foreach (var headers in invalidHeaders)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
            {
                Content = JsonContent.Create(new { reasonCode = "valid-reason" })
            };
            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);

        Assert.True(employee.IsActive);
        Assert.Equal(0, employee.LifecycleVersion);
        Assert.False(await database.EmployeeLifecycleAudits.AnyAsync(x => x.EmployeeId == employeeId));
        Assert.False(await database.OutboxMessages.AnyAsync(x => correlationIds.Contains(x.CorrelationId)));
    }

    [Fact]
    public async Task LifecycleEndpoint_AcceptsIdempotencyKeyLengthBoundariesAndRejectsInvalidCharactersWithoutMutation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var validKeys = new[] { "a", new string('z', 128) };
        foreach (var key in validKeys)
        {
            var employeeId = await SeedEmployeeAsync(factory);
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
            {
                Content = JsonContent.Create(new { reasonCode = "valid-reason" })
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);
            Assert.False(employee.IsActive);
            Assert.Equal(1, employee.LifecycleVersion);
        }

        var invalidKeys = new[] { "\u001f", "\u007f", "cl\u00e9" };
        var rejectedEmployeeIds = new List<Guid>();
        var correlationIds = new List<Guid>();

        foreach (var key in invalidKeys)
        {
            var employeeId = await SeedEmployeeAsync(factory);
            var correlationId = Guid.NewGuid();
            rejectedEmployeeIds.Add(employeeId);
            correlationIds.Add(correlationId);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
            {
                Content = JsonContent.Create(new { reasonCode = "valid-reason" })
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId.ToString());

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDatabase = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var rejectedEmployees = await verifyDatabase.Employees
            .Where(x => rejectedEmployeeIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(rejectedEmployeeIds.Count, rejectedEmployees.Count);
        Assert.All(rejectedEmployees, employee =>
        {
            Assert.True(employee.IsActive);
            Assert.Equal(0, employee.LifecycleVersion);
        });
        Assert.False(await verifyDatabase.EmployeeLifecycleAudits
            .AnyAsync(x => rejectedEmployeeIds.Contains(x.EmployeeId)));
        Assert.False(await verifyDatabase.OutboxMessages
            .AnyAsync(x => correlationIds.Contains(x.CorrelationId)));
    }

    [Fact]
    public async Task LifecycleEndpoint_RejectsInvalidReasonAndNoteWithoutMutation()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var correlationId = Guid.NewGuid();
        var invalidRequests = new[]
        {
            new { ReasonCode = "   ", Note = (string?)null },
            new { ReasonCode = new string('r', 101), Note = (string?)null },
            new { ReasonCode = "valid-reason", Note = new string('n', 1001) }
        };

        for (var index = 0; index < invalidRequests.Length; index++)
        {
            var request = invalidRequests[index];
            using var message = new HttpRequestMessage(
                HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
            {
                Content = JsonContent.Create(new
                {
                    reasonCode = request.ReasonCode,
                    note = request.Note
                })
            };
            message.Headers.Add("Idempotency-Key", $"invalid-lifecycle-{index}");
            message.Headers.Add("X-Correlation-ID", correlationId.ToString());

            using var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var employee = await database.Employees.SingleAsync(x => x.Id == employeeId);

        Assert.True(employee.IsActive);
        Assert.Equal(0, employee.LifecycleVersion);
        Assert.False(await database.EmployeeLifecycleAudits.AnyAsync(x => x.EmployeeId == employeeId));
        Assert.False(await database.OutboxMessages.AnyAsync(x => x.CorrelationId == correlationId));
    }

    [Fact]
    public async Task LifecycleEndpoint_AcceptsMaximumReasonAndNoteLengthsAndTrimsValues()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        await EnsureDatabaseAsync(client);
        var employeeId = await SeedEmployeeAsync(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));

        var reason = new string('r', 100);
        var note = new string('n', 1000);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
        {
            Content = JsonContent.Create(new
            {
                reasonCode = $"  {reason}  ",
                note
            })
        };
        request.Headers.Add("Idempotency-Key", "maximum-lifecycle-fields");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var audit = await database.EmployeeLifecycleAudits.SingleAsync(x => x.EmployeeId == employeeId);

        Assert.Equal(reason, audit.ReasonCode);
        Assert.Equal(note, audit.Note);
    }

    private async Task EnsureDatabaseAsync(HttpClient client)
    {
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedEmployeeAsync(IdentityApiFactory factory, bool isActive = true)
    {
        var id = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        database.Employees.Add(new Employee(
            id, $"{id:N}@teampulse.test", $"{id:N}@TEAMPULSE.TEST",
            "Lifecycle Test Employee", "test-password-hash",
            "org-lifecycle-test", "team-lifecycle-test", "Employee"));
        await database.SaveChangesAsync();
        if (!isActive)
        {
            await database.Employees.Where(x => x.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false));
        }
        return id;
    }

    private static Task<HttpResponseMessage> SendDeactivateAsync(
        HttpClient client, Guid employeeId, string? key, string reason)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/identity/employees/{employeeId}/deactivate")
        {
            Content = JsonContent.Create(new { reasonCode = reason, note = "integration test", actorEmployeeId = Guid.Empty })
        };
        if (key is not null)
            request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendReactivateAsync(
        HttpClient client, Guid employeeId, string reason, string key)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/identity/employees/{employeeId}/reactivate")
        {
            Content = JsonContent.Create(new { reasonCode = reason })
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string CreateToken(Guid subject, string role) =>
        CreateTokenWithRawSubject(subject.ToString(), role);

    private static string CreateTokenWithRawSubject(string? subject, string role)
    {
        var claims = new List<Claim>();
        if (subject is not null)
            claims.Add(new Claim("sub", subject));
        claims.Add(new Claim("role", role));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InvalidOperationException("Injected post-save failure."));
    }
}
