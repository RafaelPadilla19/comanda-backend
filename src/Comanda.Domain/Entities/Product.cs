using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Producto del menú / catálogo de venta.</summary>
public class Product : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Tint { get; set; } = "var(--surface-hover)";
    public string Description { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public List<ProductOption> Variants { get; set; } = new();
    public List<ProductOption> Extras { get; set; } = new();

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
}
