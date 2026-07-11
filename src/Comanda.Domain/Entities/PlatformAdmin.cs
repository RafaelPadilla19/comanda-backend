using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// Administrador de la plataforma (operador del SaaS). Vive por ENCIMA de los tenants:
/// NO es ITenantScoped y se autentica por un canal aparte (/platform/auth).
/// </summary>
public class PlatformAdmin : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
