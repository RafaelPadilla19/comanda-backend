using Comanda.Domain.Common;

namespace Comanda.Domain.Abstractions;

/// <summary>Funciones de un plan que se pueden bloquear según el nivel contratado.</summary>
public enum PlanFeature
{
    OnlinePayments,
    Coupons,
    Loyalty,
    AdvancedReports,
    Inventory,
}

/// <summary>Consumo del tenant frente a los límites de su plan (para mostrar en facturación).</summary>
public sealed class PlanUsage
{
    public bool HasPlan { get; set; }
    public string PlanName { get; set; } = "Sin plan";

    public int OrdersThisMonth { get; set; }
    public int MaxOrdersMonth { get; set; }      // 0 = ilimitado
    public decimal OveragePrice { get; set; }
    public int OverageOrders { get; set; }       // pedidos por encima del tope
    public decimal OverageAmount { get; set; }   // US$ acumulados por overage este mes
    public bool OverageAvailable { get; set; }   // el plan define precio de overage (se puede activar)
    public bool AllowOverage { get; set; }       // el dueño habilitó pedidos extra (toggle, default OFF)

    public int Branches { get; set; }
    public int MaxBranches { get; set; }
    public int Products { get; set; }
    public int MaxProducts { get; set; }
    public int Users { get; set; }
    public int MaxUsers { get; set; }

    public int RetentionMonths { get; set; }

    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }
}

/// <summary>Overage acumulado del ciclo actual que aún no se ha facturado (para sumarlo al próximo cobro).</summary>
public sealed class OverageBill
{
    public int Orders { get; set; }
    public decimal Amount { get; set; }
    public int CycleKey { get; set; }   // año*100+mes del ciclo
}

/// <summary>
/// Aplica los límites y el gating de funciones del plan del tenant actual. Si el tenant
/// no tiene plan asignado, es permisivo (no bloquea) para no dejar a nadie sin operar.
/// </summary>
public interface IPlanService
{
    /// <summary>¿Puede crear un pedido más este mes? Falla si llegó al tope y el plan no permite overage.</summary>
    Task<Result> EnsureOrderAllowedAsync(CancellationToken ct = default);

    /// <summary>Bloquea (Forbidden) si la función no está incluida en el plan.</summary>
    Task<Result> EnsureFeatureAsync(PlanFeature feature, CancellationToken ct = default);

    /// <summary>Versión booleana del gating (para condicionar comportamiento, no para devolver error).</summary>
    Task<bool> HasFeatureAsync(PlanFeature feature, CancellationToken ct = default);

    Task<Result> EnsureCanAddBranchAsync(CancellationToken ct = default);
    Task<Result> EnsureCanAddProductAsync(CancellationToken ct = default);
    Task<Result> EnsureCanAddUserAsync(CancellationToken ct = default);

    /// <summary>Consumo actual del tenant frente a su plan.</summary>
    Task<PlanUsage> GetUsageAsync(CancellationToken ct = default);

    /// <summary>Overage del ciclo actual que falta facturar; se suma al cobro de la suscripción.</summary>
    Task<OverageBill> GetUnbilledOverageAsync(CancellationToken ct = default);
}
