#nullable enable
using System;

namespace Identity.API.BackgroundServices;

public sealed class InvitationEmailOptions
{
    public const string SectionName = "InvitationEmail";
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@teampulse.local";
    public string FromName { get; set; } = "TeamPulse";
    public string ActivationBaseUrl { get; set; } = "http://localhost:5173/activate";
    public int PollIntervalSeconds { get; set; } = 3;
    public int BatchSize { get; set; } = 10;
    public int LeaseDurationSeconds { get; set; } = 60;
    public int MaxRetryDelaySeconds { get; set; } = 900;

    public void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(FromAddress) ||
            !Uri.TryCreate(ActivationBaseUrl, UriKind.Absolute, out var activationUri) ||
            (activationUri.Scheme != Uri.UriSchemeHttp && activationUri.Scheme != Uri.UriSchemeHttps) ||
            PollIntervalSeconds is < 1 or > 300 || BatchSize is < 1 or > 50 ||
            LeaseDurationSeconds is < 30 or > 600 || MaxRetryDelaySeconds is < 1 or > 3600)
            throw new InvalidOperationException("Invitation email configuration is invalid.");
    }
}
