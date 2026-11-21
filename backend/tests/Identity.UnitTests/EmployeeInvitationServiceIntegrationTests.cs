using System.Text.Json;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class EmployeeInvitationServiceIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public EmployeeInvitationServiceIntegrationTests(MySqlIntegrationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task CreateResendAndCancel_KeepTokenSecretSeparateAndRotateAtomically()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var startupResponse = await client.GetAsync("/health/live");
        startupResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"invitation-{Guid.NewGuid():N}@example.com";

        var created = await service.CreateAsync(
            Guid.NewGuid(),
            new CreateEmployeeInvitationRequest(email, "New Employee", "org-test", "team-test"),
            CancellationToken.None);

        Assert.Equal(EmployeeInvitationCommandStatus.Created, created.Status);
        var createdSummary = Assert.IsType<EmployeeInvitationSummary>(created.Invitation);
        Assert.Equal(1, createdSummary.DeliveryGeneration);
        Assert.Equal(TimeSpan.FromHours(24), createdSummary.ExpiresAtUtc - createdSummary.CreatedAtUtc);

        var duplicate = await service.CreateAsync(
            Guid.NewGuid(),
            new CreateEmployeeInvitationRequest(email.ToUpperInvariant(), "Another Name", "org-test", "team-test"),
            CancellationToken.None);
        Assert.Equal(EmployeeInvitationCommandStatus.Conflict, duplicate.Status);

        var secret = await database.InvitationDeliverySecrets.SingleAsync(
            item => item.InvitationId == createdSummary.InvitationId);
        var firstProtectedToken = secret.ProtectedToken;
        var firstToken = tokenService.Unprotect(firstProtectedToken);
        var firstDigest = tokenService.ComputeDigest(firstToken);
        var invitation = await database.EmployeeInvitations.SingleAsync(
            item => item.Id == createdSummary.InvitationId);
        Assert.Equal(firstDigest, invitation.TokenDigest);

        var createOutbox = await database.OutboxMessages
            .Where(item => item.EventType == "EmployeeInvitationDeliveryRequested" &&
                           item.Payload.Contains(createdSummary.InvitationId.ToString()))
            .OrderBy(item => item.CreatedAtUtc)
            .FirstAsync();
        using (var payload = JsonDocument.Parse(createOutbox.Payload))
        {
            Assert.Equal(createdSummary.InvitationId,
                payload.RootElement.GetProperty("invitationId").GetGuid());
            Assert.Equal(1, payload.RootElement.GetProperty("generation").GetInt32());
            Assert.DoesNotContain(firstToken, createOutbox.Payload);
            Assert.DoesNotContain(firstProtectedToken, createOutbox.Payload);
            Assert.Equal(2, payload.RootElement.EnumerateObject().Count());
        }

        var resent = await service.ResendAsync(createdSummary.InvitationId, CancellationToken.None);
        Assert.Equal(EmployeeInvitationCommandStatus.Updated, resent.Status);
        var resentSummary = Assert.IsType<EmployeeInvitationSummary>(resent.Invitation);
        Assert.Equal(2, resentSummary.DeliveryGeneration);
        Assert.True(resentSummary.ExpiresAtUtc > createdSummary.ExpiresAtUtc);

        var rotatedSecret = await database.InvitationDeliverySecrets.SingleAsync(
            item => item.InvitationId == createdSummary.InvitationId);
        Assert.NotEqual(firstProtectedToken, rotatedSecret.ProtectedToken);
        Assert.Equal(2, rotatedSecret.Generation);
        var secondToken = tokenService.Unprotect(rotatedSecret.ProtectedToken);
        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(tokenService.ComputeDigest(secondToken), invitation.TokenDigest);
        Assert.NotEqual(firstDigest, invitation.TokenDigest);

        var cancelled = await service.CancelAsync(createdSummary.InvitationId, CancellationToken.None);
        Assert.Equal(EmployeeInvitationCommandStatus.Updated, cancelled.Status);
        Assert.Equal("Cancelled", cancelled.Invitation!.Status);
        Assert.Null(invitation.TokenDigest);
        Assert.Null(invitation.PendingNormalizedEmail);
        Assert.False(await database.InvitationDeliverySecrets.AnyAsync(
            item => item.InvitationId == createdSummary.InvitationId));

        var finalSummary = await service.GetAsync(createdSummary.InvitationId, CancellationToken.None);
        Assert.Equal("Cancelled", finalSummary!.Status);
    }

    [Fact]
    public async Task Create_ReplacesExpiredPendingInvitationAndRemovesItsDeliverySecret()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        using var startupResponse = await client.GetAsync("/health/live");
        startupResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"expired-{Guid.NewGuid():N}@example.com";
        var request = new CreateEmployeeInvitationRequest(
            email, "Replacement Employee", "org-test", "team-test");

        var original = await service.CreateAsync(Guid.NewGuid(), request, CancellationToken.None);
        Assert.Equal(EmployeeInvitationCommandStatus.Created, original.Status);
        var originalId = original.Invitation!.InvitationId;

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE employee_invitations SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-1)} WHERE Id = {originalId}");
        database.ChangeTracker.Clear();

        var replacement = await service.CreateAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(EmployeeInvitationCommandStatus.Created, replacement.Status);
        Assert.NotEqual(originalId, replacement.Invitation!.InvitationId);

        var expired = await database.EmployeeInvitations.SingleAsync(item => item.Id == originalId);
        Assert.Equal("Expired", expired.Status);
        Assert.Null(expired.TokenDigest);
        Assert.Null(expired.PendingNormalizedEmail);
        Assert.False(await database.InvitationDeliverySecrets.AnyAsync(
            item => item.InvitationId == originalId));
        Assert.Equal(email, replacement.Invitation.Email);
    }
}
