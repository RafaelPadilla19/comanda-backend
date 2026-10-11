using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// País habilitado en la plataforma (catálogo GLOBAL administrado por el operador en
/// /platform/countries). NO es ITenantScoped: define en qué países se puede operar y con qué
/// valores por defecto (moneda, símbolo, impuesto); cada tenant los copia al registrarse.
/// Reemplaza la lista estática que antes vivía hardcodeada en el código (SupportedCountries).
/// </summary>
public class Country : Entity
{
    /// <summary>ISO-2 (SV, GT, HN, NI, CR, PA, CO...). Único.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;

    public string Currency { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    /// <summary>Tasa de impuesto local (IVA/ISV/ITBMS) como fracción (0.13 = 13%).</summary>
    public decimal TaxRate { get; set; } = 0.13m;
    /// <summary>Nombre local del impuesto, solo para mostrar (p.ej. "IVA", "ISV", "ITBMS").</summary>
    public string TaxLabel { get; set; } = "IVA";

    /// <summary>Si está apagado, deja de ofrecerse en registro/alta pero los tenants ya creados
    /// con este país siguen funcionando igual.</summary>
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
