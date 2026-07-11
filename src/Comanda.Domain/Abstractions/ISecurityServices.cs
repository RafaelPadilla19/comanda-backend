using Comanda.Domain.Entities;

namespace Comanda.Domain.Abstractions;

/// <summary>Hash y verificación de contraseñas.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Generación de tokens JWT para usuarios autenticados.</summary>
public interface IJwtTokenService
{
    /// <summary>Devuelve (token, expiración UTC) para un usuario de un restaurante.</summary>
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);

    /// <summary>Token para un administrador de plataforma (rol "PlatformAdmin", sin tenant).</summary>
    (string Token, DateTime ExpiresAtUtc) CreatePlatformToken(PlatformAdmin admin);
}
