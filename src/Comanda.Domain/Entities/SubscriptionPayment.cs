using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Pago de la suscripción del restaurante al SaaS (restaurante → Innovacors).</summary>
public class SubscriptionPayment : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int PeriodMonths { get; set; } = 1;

    /// <summary>Porción de <see cref="Amount"/> que corresponde a pedidos extra (overage) de este cobro.</summary>
    public decimal OverageAmount { get; set; }
    /// <summary>Cantidad de pedidos extra incluidos en este cobro.</summary>
    public int OverageOrders { get; set; }
    /// <summary>Ciclo (año*100+mes) de los pedidos extra cobrados; lo usa el callback para marcarlos facturados.</summary>
    public int OverageCycleKey { get; set; }

    public bool IsPaid { get; set; }
    public string PaymentRef { get; set; } = string.Empty;  // chargeId en PaymentsHub
    public DateTime? PaidAt { get; set; }
}
