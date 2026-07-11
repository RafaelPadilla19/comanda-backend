using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Insumo de almacén con stock, mínimo y costo.</summary>
public class InventoryItem : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public string Unit { get; set; } = string.Empty;     // kg, L, unidad
    public decimal Min { get; set; }
    public decimal Cost { get; set; }
}
