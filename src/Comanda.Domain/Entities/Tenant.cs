using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>
/// Restaurante cliente del SaaS (raíz del aislamiento multi-tenant). NO es ITenantScoped:
/// es el dueño del tenant, no algo perteneciente a un tenant.
/// </summary>
public class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Identificador legible y único para URLs (p.ej. "sabores-del-puerto").</summary>
    public string Slug { get; set; } = string.Empty;
    /// <summary>País del restaurante (ISO-2: SV, GT, HN, NI, CR, PA). Lo elige el dueño al registrarse; sin valor por defecto.</summary>
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Suscripción al SaaS (nivel plataforma)
    public Guid? PlanId { get; set; }
    public Plan? Plan { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }   // pagada hasta (renovación)

    /// <summary>
    /// ¿El dueño habilitó pedidos extra sobre el límite del plan? Apagado por defecto.
    /// Si está activo Y el plan tiene precio de overage, se permite superar el tope y se cobra el excedente.
    /// </summary>
    public bool AllowOverage { get; set; } = false;

    /// <summary>Pedidos extra (overage) ya facturados en el ciclo <see cref="OverageCycleKey"/>; evita cobrar dos veces.</summary>
    public int OverageBilledOrders { get; set; }
    /// <summary>Ciclo (año*100+mes) al que pertenece <see cref="OverageBilledOrders"/>. Si cambia el mes, lo facturado se considera 0.</summary>
    public int OverageCycleKey { get; set; }

    // Pagos en línea propios del restaurante (Wompi). Secret cifrado en reposo.
    public string WompiAppId { get; set; } = string.Empty;
    public string WompiApiSecretEnc { get; set; } = string.Empty;
    public bool HasOwnPayments => !string.IsNullOrEmpty(WompiAppId) && !string.IsNullOrEmpty(WompiApiSecretEnc);

    // Fidelización (loyalty)
    public bool LoyaltyEnabled { get; set; }
    /// <summary>Puntos ganados por cada US$1 gastado.</summary>
    public decimal LoyaltyEarnRate { get; set; } = 1m;
    /// <summary>Puntos necesarios para US$1 de descuento al canjear.</summary>
    public int LoyaltyRedeemRate { get; set; } = 20;
}
