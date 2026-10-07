using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Token de renovación de sesión (vida larga) para la app. Permite emitir un JWT
/// nuevo sin pedir credenciales otra vez, mientras siga vigente y no haya sido revocado.
/// Se rota en cada uso: el usado se revoca y se enlaza al nuevo vía ReplacedByToken (evita
/// reuso de un token ya consumido, por ejemplo uno robado de un log).</summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
}
