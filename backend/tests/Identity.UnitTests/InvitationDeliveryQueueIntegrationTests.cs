using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.UnitTests;

public sealed class InvitationDeliveryQueueIntegrationTests : IClassFixture<MySqlIntegrationFixture>
{
    private readonly MySqlIntegrationFixture _fixture;

    public InvitationDeliveryQueueIntegrationTests(MySqlIntegrationFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task Claim_RetryAndResend_OnlyCurrentGenerationCanBeAcknowledged()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        var ready = await client.GetAsync("/health/ready");
        Assert.True(ready.IsSuccessStatusCode);

        Guid invitationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
            var created = await service.CreateAsync(
                Guid.NewGuid(),
                new CreateEmployeeInvitationRequest(
                    $"delivery-{Guid.NewGuid():N}@example.com", "Delivery Test", "org", "team"),
                CancellationToken.None);
            Assert.Equal(EmployeeInvitationCommandStatus.Created, created.Status);
            invitationId = created.Invitation!.InvitationId;
        }

        InvitationDeliveryWorkItem first;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IInvitationDeliveryQueue>();
            var claimed = await queue.ClaimBatchAsync(10, TimeSpan.FromSeconds(60), CancellationToken.None);
            first = Assert.Single(claimed, item => item.InvitationId == invitationId);
            Assert.Equal(1, first.Generation);

            var scheduled = await queue.ScheduleRetryAsync(
                first.SecretId, first.Generation, first.LeaseToken,
                DateTime.UtcNow.AddMinutes(2), "SmtpDeliveryFailed", CancellationToken.None);
            Assert.True(scheduled);

            var notDue = await queue.ClaimBatchAsync(10, TimeSpan.FromSeconds(60), CancellationToken.None);
            Assert.DoesNotContain(notDue, item => item.InvitationId == invitationId);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
            var resent = await service.ResendAsync(invitationId, CancellationToken.None);
            Assert.Equal(EmployeeInvitationCommandStatus.Updated, resent.Status);
            Assert.Equal(2, resent.Invitation!.DeliveryGeneration);
        }

        InvitationDeliveryWorkItem second;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IInvitationDeliveryQueue>();
            var claimed = await queue.ClaimBatchAsync(10, TimeSpan.FromSeconds(60), CancellationToken.None);
            second = Assert.Single(claimed, item => item.InvitationId == invitationId);
            Assert.Equal(2, second.Generation);

            Assert.False(await queue.MarkAcceptedAsync(
                first.SecretId, first.Generation, first.LeaseToken, CancellationToken.None));
            Assert.True(await queue.MarkAcceptedAsync(
                second.SecretId, second.Generation, second.LeaseToken, CancellationToken.None));
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var database = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await database.InvitationDeliverySecrets.AnyAsync(
            item => item.InvitationId == invitationId));
        var invitation = await database.EmployeeInvitations.SingleAsync(item => item.Id == invitationId);
        Assert.Equal("Pending", invitation.Status);
        Assert.NotNull(invitation.TokenDigest);
    }

    [Fact]
    public async Task ExpirePending_RemovesProtectedSecretAndInvalidatesTokenDigest()
    {
        using var factory = _fixture.CreateFactory();
        using var client = factory.CreateClient();
        var ready = await client.GetAsync("/health/ready");
        Assert.True(ready.IsSuccessStatusCode);

        Guid invitationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeInvitationService>();
            var created = await service.CreateAsync(
                Guid.NewGuid(),
                new CreateEmployeeInvitationRequest(
                    $"expired-delivery-{Guid.NewGuid():N}@example.com", "Expired Delivery", "org", "team"),
                CancellationToken.None);
            invitationId = created.Invitation!.InvitationId;
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE employee_invitations SET ExpiresAtUtc = {DateTime.UtcNow.AddMinutes(-1)} WHERE Id = {invitationId}");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IInvitationDeliveryQueue>();
            Assert.True(await queue.ExpirePendingAsync(CancellationToken.None) >= 1);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var invitation = await verify.EmployeeInvitations.SingleAsync(item => item.Id == invitationId);
        Assert.Equal("Expired", invitation.Status);
        Assert.Null(invitation.TokenDigest);
        Assert.Null(invitation.PendingNormalizedEmail);
        Assert.False(await verify.InvitationDeliverySecrets.AnyAsync(
            item => item.InvitationId == invitationId));
    }
}
