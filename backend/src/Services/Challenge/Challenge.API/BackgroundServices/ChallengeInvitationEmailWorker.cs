using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Challenge.Domain.Entities;
using Challenge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Challenge.API.BackgroundServices;

public sealed class ChallengeInvitationEmailWorker(
    IDbContextFactory<ChallengeDbContext> contextFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<ChallengeInvitationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!configuration.GetValue<bool>("ChallengeInvitationEmail:Enabled"))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            var processed = await DispatchBatch(stoppingToken);
            if (processed == 0)
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task<int> DispatchBatch(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var invitations = await db.ChallengeInvitations
            .Where(x => x.EmailDeliveryStatus == "Pending")
            .OrderBy(x => x.InvitedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        if (invitations.Count == 0) return 0;

        var host = configuration["ChallengeInvitationEmail:Host"] ?? "localhost";
        var port = configuration.GetValue<int?>("ChallengeInvitationEmail:Port") ?? 1025;
        var fromAddress = configuration["ChallengeInvitationEmail:FromAddress"] ?? "no-reply@teampulse.local";
        var fromName = configuration["ChallengeInvitationEmail:FromName"] ?? "TeamPulse";
        var appUrl = configuration["ChallengeInvitationEmail:ApplicationBaseUrl"] ?? "http://localhost:5173/challenges";

        foreach (var invitation in invitations)
        {
            try
            {
                var challenge = await db.Challenges.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == invitation.ChallengeId, cancellationToken);
                if (challenge is null)
                {
                    invitation.MarkEmailFailed();
                    continue;
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = $"You're invited: {challenge.Name}",
                    Body = BuildBody(invitation.DisplayName, challenge, appUrl),
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress(invitation.Email));
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = configuration.GetValue<bool>("ChallengeInvitationEmail:EnableSsl"),
                    Timeout = 15000
                };
                var userName = configuration["ChallengeInvitationEmail:UserName"];
                if (!string.IsNullOrWhiteSpace(userName))
                    client.Credentials = new System.Net.NetworkCredential(userName, configuration["ChallengeInvitationEmail:Password"]);
                await client.SendMailAsync(message, cancellationToken);
                invitation.MarkEmailSent(timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                invitation.MarkEmailFailed();
                logger.LogWarning(exception, "Challenge invitation email failed for employee {EmployeeId}, challenge {ChallengeId}.",
                    invitation.EmployeeId, invitation.ChallengeId);
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        return invitations.Count;
    }

    private static string BuildBody(string displayName, Challenge.Domain.Entities.Challenge challenge, string appUrl) =>
        $"<html><body><p>Hello {WebUtility.HtmlEncode(displayName)},</p>" +
        $"<p>HR has invited you to <strong>{WebUtility.HtmlEncode(challenge.Name)}</strong>.</p>" +
        $"<p>{WebUtility.HtmlEncode(challenge.Description ?? string.Empty)}</p>" +
        $"<p>Challenge dates: {challenge.StartDate:yyyy-MM-dd} to {challenge.EndDate:yyyy-MM-dd}</p>" +
        $"<p>Respond by: {challenge.RegistrationDeadline:yyyy-MM-dd}</p>" +
        $"<p><a href=\"{WebUtility.HtmlEncode(appUrl + (appUrl.Contains('?') ? "&" : "?") + "challengeId=" + challenge.Id)}\">Open TeamPulse to Join or Decline</a></p></body></html>";
}