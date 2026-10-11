using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>Línea de kardex: un movimiento de stock de un insumo, con quién y por qué (feature
/// de plan Elite, igual que el resto de Inventario). `Delta` ya trae el signo (positivo = entrada,
/// negativo = salida/merma); `ResultingStock` es una foto del stock justo después del movimiento,
/// para poder reconstruir el historial sin recalcular sumando todo lo anterior.</summary>
public class InventoryMovement : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public InventoryMovementReason Reason { get; set; }
    public decimal Delta { get; set; }
    public decimal ResultingStock { get; set; }
    public string Note { get; set; } = string.Empty;
    /// <summary>Nombre del usuario que hizo el movimiento (denormalizado: si el usuario se
    /// desactiva o borra, el kardex no debe perder el rastro de quién fue).</summary>
    public string CreatedByName { get; set; } = string.Empty;
}
