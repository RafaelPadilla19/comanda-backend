using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Configuración de un método de pago habilitable.</summary>
public class PaymentMethodConfig : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;       // pay-efectivo, pay-tarjeta, …
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
}
