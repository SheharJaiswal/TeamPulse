#nullable enable
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Messaging;

public sealed class SmtpInvitationEmailSender : IInvitationEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly bool _enableSsl;
    private readonly string? _userName;
    private readonly string? _password;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly Uri _activationBaseUri;

    public SmtpInvitationEmailSender(
        string host, int port, bool enableSsl, string? userName, string? password,
        string fromAddress, string fromName, string activationBaseUrl)
    {
        _host = host;
        _port = port;
        _enableSsl = enableSsl;
        _userName = userName;
        _password = password;
        _fromAddress = fromAddress;
        _fromName = fromName;
        _activationBaseUri = new Uri(activationBaseUrl, UriKind.Absolute);
    }

    public async Task SendAsync(
        string email, string displayName, string activationUrl,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = "Activate your TeamPulse account",
            Body = BuildBody(displayName, activationUrl),
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = _enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        if (!string.IsNullOrWhiteSpace(_userName))
            client.Credentials = new NetworkCredential(_userName, _password);

        await client.SendMailAsync(message, cancellationToken);
    }

    public string CreateActivationUrl(string token)
    {
        var builder = new UriBuilder(_activationBaseUri);
        var query = builder.Query.TrimStart('?');
        var tokenParameter = "token=" + Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrEmpty(query) ? tokenParameter : query + "&" + tokenParameter;
        return builder.Uri.ToString();
    }

    private static string BuildBody(string displayName, string activationUrl)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeUrl = WebUtility.HtmlEncode(activationUrl);
        return $"""
            <html><body>
              <p>Hello {safeName},</p>
              <p>You have been invited to join TeamPulse.</p>
              <p><a href="{safeUrl}">Activate your account</a></p>
              <p>This invitation expires in 24 hours. If you were not expecting it, you can ignore this email.</p>
            </body></html>
            """;
    }
}
