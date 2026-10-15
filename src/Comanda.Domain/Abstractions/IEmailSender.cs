namespace Comanda.Domain.Abstractions;

/// <summary>Envío de correo transaccional (OTP, notificaciones). Un solo remitente SMTP
/// global de la plataforma — a diferencia de los pagos, el correo no es por-tenant.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
