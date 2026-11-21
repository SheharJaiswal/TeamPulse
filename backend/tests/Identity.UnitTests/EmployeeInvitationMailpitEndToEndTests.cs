using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.IdentityModel.Tokens;
using Identity.API.BackgroundServices;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Identity.UnitTests;

public sealed class EmployeeInvitationMailpitEndToEndTests : IClassFixture<MySqlIntegrationFixture>
{
    private const string Issuer = "TeamPulse.Identity.IntegrationTests";
    private const string Audience = "TeamPulse.Services.IntegrationTests";
    private const string SigningKey = "integration-test-signing-key-at-least-32-bytes";
    private readonly MySqlIntegrationFixture _fixture;

    public EmployeeInvitationMailpitEndToEndTests(MySqlIntegrationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task HrInvitation_IsDeliveredToMailpit_ResendInvalidatesOldLink_AndNewLinkActivatesEmployee()
    {
        await using var mailpit = new ContainerBuilder()
            .WithImage("axllent/mailpit:v1.27.8")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1025))
            .Build();
        await mailpit.StartAsync();

        var smtpPort = mailpit.GetMappedPublicPort(1025);
        var apiPort = mailpit.GetMappedPublicPort(8025);
        var settings = new Dictionary<string, string?>
        {
            ["InvitationEmail:Enabled"] = "true",
            ["InvitationEmail:Host"] = "127.0.0.1",
            ["InvitationEmail:Port"] = smtpPort.ToString(),
            ["InvitationEmail:EnableSsl"] = "false",
            ["InvitationEmail:FromAddress"] = "no-reply@teampulse.local",
            ["InvitationEmail:FromName"] = "TeamPulse",
            ["InvitationEmail:ActivationBaseUrl"] = "http://localhost:5173/activate",
            ["InvitationEmail:PollIntervalSeconds"] = "1",
            ["InvitationEmail:BatchSize"] = "5",
            ["InvitationEmail:LeaseDurationSeconds"] = "30",
            ["InvitationEmail:MaxRetryDelaySeconds"] = "30"
        };

        using var factory = new IdentityApiFactory(_fixture.ConnectionString, "Development", settings);
        using var client = factory.CreateClient();
        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            Assert.True(scope.ServiceProvider.GetRequiredService<IOptions<InvitationEmailOptions>>().Value.Enabled);
            Assert.Contains(scope.ServiceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>(),
                service => service is InvitationDeliveryWorker);
        }

        var email = $"mailpit-{Guid.NewGuid():N}@example.com";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid(), "HrAdmin"));
        var create = await client.PostAsJsonAsync("/api/identity/invitations", new
        {
            email,
            displayName = "Mailpit Employee",
            organizationId = "org-mailpit",
            teamId = "team-mailpit"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", createBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("digest", createBody, StringComparison.OrdinalIgnoreCase);
        using var createdDocument = JsonDocument.Parse(createBody);
        var invitationId = createdDocument.RootElement.GetProperty("invitationId").GetGuid();

        using var mailpitClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{apiPort}")
        };
        string firstToken;
        try
        {
            firstToken = await WaitForInvitationTokenAsync(mailpitClient, email);
        }
        catch (TimeoutException)
        {
            await using var diagnosticScope = factory.Services.CreateAsyncScope();
            var database = diagnosticScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var secret = await database.InvitationDeliverySecrets
                .SingleOrDefaultAsync(item => item.InvitationId == invitationId);
            var mailpitStatus = await mailpitClient.GetStringAsync("/api/v1/messages");
            var messageCount = JsonDocument.Parse(mailpitStatus).RootElement
                .GetProperty("total").GetInt32();
            throw new Xunit.Sdk.XunitException(
                $"Invitation delivery did not reach Mailpit. Attempts={secret?.AttemptCount}, " +
                $"LastErrorCode={secret?.LastErrorCode ?? "none"}, LeaseActive={secret?.LeaseExpiresAtUtc > DateTime.UtcNow}, " +
                $"MailpitMessageCount={messageCount}.");
        }
        Assert.Matches("^[A-Za-z0-9_-]{43}$", firstToken);

        var resend = await client.PostAsync($"/api/identity/invitations/{invitationId}/resend", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var secondToken = await WaitForInvitationTokenAsync(mailpitClient, email, expectedMessages: 2);
        Assert.NotEqual(firstToken, secondToken);

        client.DefaultRequestHeaders.Authorization = null;
        var oldActivation = await client.PostAsJsonAsync(
            "/api/identity/invitations/activate",
            new { token = firstToken, password = "A-secure-password-123!" });
        Assert.Equal(HttpStatusCode.BadRequest, oldActivation.StatusCode);
        Assert.Contains("invalid or can no longer be used",
            await oldActivation.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var activation = await client.PostAsJsonAsync(
            "/api/identity/invitations/activate",
            new { token = secondToken, password = "A-secure-password-123!" });
        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);

        var login = await client.PostAsJsonAsync("/api/identity/login", new
        {
            email,
            password = "A-secure-password-123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("Employee",
            loginDocument.RootElement.GetProperty("employee").GetProperty("role").GetString());
    }

    private static async Task<string> WaitForInvitationTokenAsync(
        HttpClient mailpitClient, string recipient, int expectedMessages = 1)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var listResponse = await mailpitClient.GetAsync("/api/v1/messages");
            listResponse.EnsureSuccessStatusCode();
            using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
            var messages = listDocument.RootElement.GetProperty("messages");
            var matching = messages.EnumerateArray()
                .Where(message => HasRecipient(message, recipient))
                .ToArray();

            if (matching.Length >= expectedMessages)
            {
                var messageId = matching
                    .OrderBy(message => message.TryGetProperty("Created", out var created)
                        ? created.GetString() : string.Empty)
                    .Last()
                    .GetProperty("ID").GetString();

                using var detailResponse = await mailpitClient.GetAsync(
                    $"/api/v1/message/{Uri.EscapeDataString(messageId!)}");
                detailResponse.EnsureSuccessStatusCode();
                using var detailDocument = JsonDocument.Parse(
                    await detailResponse.Content.ReadAsStringAsync());
                var root = detailDocument.RootElement;
                var body = root.TryGetProperty("HTML", out var html) ? html.GetString() ?? string.Empty :
                    root.TryGetProperty("Text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
                var match = Regex.Match(body, @"token=([A-Za-z0-9_-]{43})");
                if (match.Success)
                    return match.Groups[1].Value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Mailpit did not receive {expectedMessages} invitation message(s) for {recipient}.");
    }

    private static bool HasRecipient(JsonElement message, string recipient)
    {
        if (!message.TryGetProperty("To", out var recipients))
            return false;

        if (recipients.ValueKind == JsonValueKind.Array)
        {
            return recipients.EnumerateArray().Any(address =>
            {
                if (address.ValueKind == JsonValueKind.String)
                    return address.GetString()?.Contains(recipient, StringComparison.OrdinalIgnoreCase) == true;

                return (address.TryGetProperty("Address", out var value) ||
                        address.TryGetProperty("address", out value)) &&
                       value.GetString()?.Equals(recipient, StringComparison.OrdinalIgnoreCase) == true;
            });
        }

        return recipients.ToString().Contains(recipient, StringComparison.OrdinalIgnoreCase);
    }

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
