namespace Comanda.Infrastructure.Security;

/// <summary>Opciones de firma/validez del JWT. La clave se inyecta por configuración/entorno.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Comanda";
    public string Audience { get; set; } = "ComandaClients";
    public int ExpiryMinutes { get; set; } = 480; // 8 horas
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
