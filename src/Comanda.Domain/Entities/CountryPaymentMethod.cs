using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// Método de pago disponible en un país (catálogo GLOBAL administrado por el operador en
/// /platform). NO es ITenantScoped: define qué métodos EXISTEN por país; cada restaurante
/// luego activa/desactiva los de su país (PaymentMethodConfig).
/// </summary>
public class CountryPaymentMethod : Entity
{
    /// <summary>País ISO-2 (SV, GT, HN, NI, CR, PA).</summary>
    public string Country { get; set; } = string.Empty;
    /// <summary>Clave estable del método (p.ej. "pay-transfer365", "pay-online").</summary>
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = "💳";
    /// <summary>true = pago en línea (Wompi); requiere que el plan incluya OnlinePayments.</summary>
    public bool IsOnline { get; set; }
    /// <summary>Si el método viene activado por defecto al provisionar un restaurante del país.</summary>
    public bool DefaultEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
