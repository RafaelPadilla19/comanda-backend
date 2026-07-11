using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Categoría del menú (Entradas, Fondos, Bebidas, Postres, …).</summary>
public class Category : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
