using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Zona de entrega con su tarifa de envío (delivery autogestionado, sin comisión).</summary>
public class DeliveryZone : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    /// <summary>Sucursal dueña de la zona. Null = aplica a todas las sucursales.</summary>
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public bool IsActive { get; set; } = true;
}
