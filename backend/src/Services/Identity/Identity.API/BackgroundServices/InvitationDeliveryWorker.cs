using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.API.BackgroundServices;

public sealed class InvitationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IInvitationEmailSender emailSender,
    IOptions<InvitationEmailOptions> options,
    TimeProvider timeProvider,
    ILogger<InvitationDeliveryWorker> logger) : BackgroundService
{
    private readonly InvitationEmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IInvitationDeliveryQueue>();
                var tokenService = scope.ServiceProvider.GetRequiredService<IInvitationTokenService>();

                await queue.ExpirePendingAsync(stoppingToken);
                var batch = await queue.ClaimBatchAsync(
                    _options.BatchSize,
                    TimeSpan.FromSeconds(_options.LeaseDurationSeconds),
                    stoppingToken);

                foreach (var item in batch)
                {
                    try
                    {
                        var token = tokenService.Unprotect(item.ProtectedToken);
                        var activationUrl = CreateActivationUrl(token);
                        await emailSender.SendAsync(
                            item.Email, item.DisplayName, activationUrl, stoppingToken);

                        if (await queue.MarkAcceptedAsync(
                            item.SecretId, item.Generation, item.LeaseToken, stoppingToken))
                        {
                            logger.LogInformation(
                                "Invitation email accepted by SMTP server. InvitationId={InvitationId} Generation={Generation}",
                                item.InvitationId, item.Generation);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Invitation delivery acknowledgement skipped because the generation or lease changed. InvitationId={InvitationId} Generation={Generation}",
                                item.InvitationId, item.Generation);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        var retrySeconds = RetryDelay(item.AttemptCount);
                        await queue.ScheduleRetryAsync(
                            item.SecretId, item.Generation, item.LeaseToken,
                            timeProvider.GetUtcNow().UtcDateTime.AddSeconds(retrySeconds),
                            ErrorCode(exception), stoppingToken);
                        logger.LogWarning(
                            "Invitation email delivery failed and was scheduled for retry. InvitationId={InvitationId} Generation={Generation} ErrorType={ErrorType} RetrySeconds={RetrySeconds}",
                            item.InvitationId, item.Generation, exception.GetType().Name, retrySeconds);
                    }
                }

                if (batch.Count > 0)
                    delay = TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Invitation delivery worker iteration failed. ErrorType={ErrorType}",
                    exception.GetType().Name);
            }

            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    private string CreateActivationUrl(string token)
    {
        var builder = new UriBuilder(_options.ActivationBaseUrl);
        var query = builder.Query.TrimStart('?');
        var tokenParameter = "token=" + Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrEmpty(query) ? tokenParameter : query + "&" + tokenParameter;
        return builder.Uri.ToString();
    }

    private int RetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var seconds = Math.Min(_options.MaxRetryDelaySeconds, Math.Pow(2, exponent) * 5);
        return Math.Max(1, (int)seconds);
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        CryptographicException => "ProtectedSecretUnavailable",
        SmtpException => "SmtpDeliveryFailed",
        _ => "DeliveryFailed"
    };
}
