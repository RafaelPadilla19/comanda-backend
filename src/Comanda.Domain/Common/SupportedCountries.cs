namespace Comanda.Domain.Common;

/// <summary>
/// Países soportados por la plataforma y sus valores por defecto (moneda, símbolo, IVA/impuesto
/// de venta local). Única fuente de verdad: registro, Configuración y el seed de métodos de pago
/// por país deben usar esta lista en vez de tener su propio arreglo hardcodeado (antes se
/// duplicaba en 3 lugares y era fácil olvidar actualizar uno al agregar un país nuevo).
/// </summary>
public static class SupportedCountries
{
    public sealed record Defaults(string Currency, string CurrencySymbol, decimal TaxRate);

    public static readonly IReadOnlyDictionary<string, Defaults> All = new Dictionary<string, Defaults>
    {
        ["SV"] = new Defaults("USD", "$", 0.13m),   // IVA 13%
        ["GT"] = new Defaults("GTQ", "Q", 0.12m),   // IVA 12%
        ["HN"] = new Defaults("HNL", "L", 0.15m),   // ISV 15%
        ["NI"] = new Defaults("NIO", "C$", 0.15m),  // IVA 15%
        ["CR"] = new Defaults("CRC", "₡", 0.13m),   // IVA 13%
        ["PA"] = new Defaults("USD", "$", 0.07m),   // ITBMS 7%
        ["CO"] = new Defaults("COP", "$", 0.19m),   // IVA 19%
    };

    public static string[] Codes => All.Keys.ToArray();

    public static bool IsValid(string code) => All.ContainsKey(code);

    /// <summary>Valores por defecto al registrar un tenant nuevo; si el país no está en la lista
    /// (no debería pasar, ya se valida antes), cae a USD/13% en vez de reventar.</summary>
    public static Defaults DefaultsFor(string code) =>
        All.TryGetValue(code, out var d) ? d : new Defaults("USD", "$", 0.13m);
}
