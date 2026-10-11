using Comanda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

/// <summary>
/// Lecturas del catálogo de países (tabla <see cref="Country"/>, administrada en
/// /platform/countries). Reemplaza la antigua lista estática `SupportedCountries`: ahora el
/// operador puede agregar/editar/desactivar países sin tocar código ni redeployar.
/// </summary>
public static class CountryCatalog
{
    public sealed record Defaults(string Currency, string CurrencySymbol, decimal TaxRate);

    public static Task<bool> IsValidAsync(ComandaDbContext db, string code, CancellationToken ct) =>
        db.Countries.AnyAsync(c => c.Code == code && c.IsActive, ct);

    /// <summary>Valores por defecto al registrar un tenant nuevo; si el país no está en el
    /// catálogo (no debería pasar, ya se valida antes), cae a USD/13% en vez de reventar.</summary>
    public static async Task<Defaults> DefaultsForAsync(ComandaDbContext db, string code, CancellationToken ct)
    {
        var c = await db.Countries.FirstOrDefaultAsync(x => x.Code == code, ct);
        return c is null ? new Defaults("USD", "$", 0.13m) : new Defaults(c.Currency, c.CurrencySymbol, c.TaxRate);
    }

    public static Task<string[]> ActiveCodesAsync(ComandaDbContext db, CancellationToken ct) =>
        db.Countries.Where(c => c.IsActive).OrderBy(c => c.SortOrder).Select(c => c.Code).ToArrayAsync(ct);
}
