using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// Configuración global del operador del SaaS (fila única). NO es ITenantScoped:
/// vive por encima de los tenants. Aquí van las llaves Wompi de Innovacors con las
/// que se cobran las suscripciones de los restaurantes (secret cifrado en reposo).
/// </summary>
public class PlatformSetting : Entity
{
    public string WompiAppId { get; set; } = string.Empty;
    public string WompiApiSecretEnc { get; set; } = string.Empty;
    public bool HasWompi => !string.IsNullOrEmpty(WompiAppId) && !string.IsNullOrEmpty(WompiApiSecretEnc);
}
