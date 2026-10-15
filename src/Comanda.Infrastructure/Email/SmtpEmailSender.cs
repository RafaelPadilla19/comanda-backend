using MailKit.Net.Smtp;
using MailKit.Security;
using Comanda.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Comanda.Infrastructure.Email;

/// <summary>Implementación SMTP vía MailKit (mismo mecanismo que POS). Configuración por
/// variables de entorno — nunca hardcodeada: Smtp__Host, Smtp__Port, Smtp__Username,
/// Smtp__Password, Smtp__FromName, Smtp__UseSsl.</summary>
public sealed class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = config["Smtp:Host"] ?? throw new InvalidOperationException(
            "Falta 'Smtp:Host' (configúralo por variable de entorno Smtp__Host).");
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        var username = config["Smtp:Username"] ?? string.Empty;
        var password = config["Smtp:Password"] ?? string.Empty;
        var fromName = config["Smtp:FromName"] ?? "Comanda";
        var useSsl = bool.TryParse(config["Smtp:UseSsl"], out var ssl) && ssl;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, username));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var secure = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(host, port, secure, ct);
        await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
