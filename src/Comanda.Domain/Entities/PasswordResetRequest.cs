using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Flujo de "olvidé mi contraseña" en 3 pasos: OTP por email → canje por un token de
/// un solo uso → reset con ese token. No es ITenantScoped (se busca por email antes de saber
/// a qué tenant pertenece el usuario, igual que Login/Register).</summary>
public class PasswordResetRequest : Entity
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 del código OTP (nunca se guarda en claro).</summary>
    public string OtpHash { get; set; } = string.Empty;
    public DateTime OtpExpiresAt { get; set; }
    public int Attempts { get; set; }

    /// <summary>Se llena al verificar el OTP correctamente; canjeable una sola vez por
    /// ResetPasswordEndpoint. Opaco (no es el OTP) para que el OTP no sirva dos veces.</summary>
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }

    public bool Used { get; set; }
}
