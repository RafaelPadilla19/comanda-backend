using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// Plan de suscripción del SaaS (nivel plataforma, NO por tenant). Define precio
/// mensual, límites y funciones incluidas. Todo es editable desde la consola del
/// operador (/platform). En los límites, 0 = ilimitado.
/// </summary>
public class Plan : Entity
{
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }

    // ---- Límites (0 = ilimitado) ----
    public int MaxBranches { get; set; }
    public int MaxProducts { get; set; }
    public int MaxUsers { get; set; }
    /// <summary>Pedidos permitidos por mes calendario. 0 = ilimitado.</summary>
    public int MaxOrdersMonth { get; set; }

    /// <summary>Precio por pedido extra sobre el tope mensual. 0 = bloquear al llegar al tope (sin overage).</summary>
    public decimal OveragePrice { get; set; }

    /// <summary>Meses de historial de pedidos que se conservan; lo anterior se purga. 0 = sin límite.</summary>
    public int RetentionMonths { get; set; }

    // ---- Funciones incluidas (gating) ----
    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
