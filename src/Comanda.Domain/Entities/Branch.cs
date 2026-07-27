using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Sucursal del restaurante.</summary>
public class Branch : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    /// <summary>WhatsApp del restaurante con código de país, solo dígitos (p.ej. "50370001111").</summary>
    public string WhatsappPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Ubicación exacta del pin de recogida (elegido en el mapa). Null = solo se conoce la dirección en texto.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
