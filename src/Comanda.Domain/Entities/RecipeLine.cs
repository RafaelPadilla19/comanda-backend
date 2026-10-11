namespace Comanda.Domain.Entities;

/// <summary>Línea de receta (BOM): cuánto de un insumo consume una unidad de un producto.
/// Value object embebido en <see cref="Product"/> (igual que <see cref="ProductOption"/>), no
/// una tabla aparte — la receta es config del producto, no algo que se consulte por su cuenta.</summary>
public class RecipeLine
{
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
}
