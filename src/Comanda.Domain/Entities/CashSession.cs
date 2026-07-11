using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>Sesión de caja del día (arqueo) por sucursal y cajero.</summary>
public class CashSession : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public decimal SalesEfectivo { get; set; }
    public decimal SalesTarjeta { get; set; }
    public decimal SalesTransfer365 { get; set; }
    public decimal SalesTransfer { get; set; }

    public List<CashMovement> Movements { get; set; } = new();
}

/// <summary>Movimiento de efectivo (ingreso/egreso/fondo) dentro de una sesión.</summary>
public class CashMovement : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CashSessionId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Sub { get; set; } = string.Empty;
    public decimal Amount { get; set; }           // positivo = ingreso, negativo = egreso
    public MovementType Type { get; set; }
}
