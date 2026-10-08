using System.Text;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

public sealed class TenantProvisioningService(
    ComandaDbContext db, ICurrentTenant tenant, IPasswordHasher hasher) : ITenantProvisioningService
{
    private static readonly string[] SupportedCountries = ["SV", "GT", "HN", "NI", "CR", "PA"];

    public async Task<Result<User>> RegisterAsync(
        string restaurantName, string adminName, string email, string password, string country, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // El email es único global (el login es por email): se valida ignorando el filtro.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail, ct))
            return Error.Conflict("auth.email_existe", "Ya existe una cuenta con ese correo.");

        var code = (country ?? string.Empty).Trim().ToUpperInvariant();
        if (!SupportedCountries.Contains(code))
            return Error.Validation("registro.pais", "Selecciona un país válido.");

        var newTenant = new Tenant { Name = restaurantName.Trim(), Slug = await UniqueSlugAsync(restaurantName, ct), Country = code };

        // Plan inicial: el más económico activo (Starter, freemium). Queda activo de inmediato.
        var starter = await db.Plans.Where(p => p.IsActive)
            .OrderBy(p => p.PriceMonthly).ThenBy(p => p.SortOrder).FirstOrDefaultAsync(ct);
        if (starter is not null)
        {
            newTenant.PlanId = starter.Id;
            newTenant.SubscriptionStatus = SubscriptionStatus.Active;
        }

        db.Tenants.Add(newTenant);
        await db.SaveChangesAsync(ct);

        tenant.Set(newTenant.Id); // a partir de aquí el TenantId se estampa solo

        var admin = new User
        {
            Name = adminName.Trim(),
            Email = normalizedEmail,
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Administradora,
            IsActive = true,
            Tenant = newTenant,
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        await DbSeeder.SeedStarterAsync(db, code, ct);

        return admin;
    }

    /// <summary>Genera un slug único a partir del nombre del restaurante.</summary>
    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "restaurante";

        var slug = baseSlug;
        var n = 1;
        while (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug, ct))
            slug = $"{baseSlug}-{++n}";

        return slug;
    }

    private static string Slugify(string text)
    {
        var sb = new StringBuilder();
        var prevDash = false;
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                sb.Append(ch);
                prevDash = false;
            }
            else if (ch is ' ' or '-' or '_' && !prevDash && sb.Length > 0)
            {
                sb.Append('-');
                prevDash = true;
            }
            else if (ch is 'á' or 'à' or 'ä') { sb.Append('a'); prevDash = false; }
            else if (ch is 'é' or 'è' or 'ë') { sb.Append('e'); prevDash = false; }
            else if (ch is 'í' or 'ì' or 'ï') { sb.Append('i'); prevDash = false; }
            else if (ch is 'ó' or 'ò' or 'ö') { sb.Append('o'); prevDash = false; }
            else if (ch is 'ú' or 'ù' or 'ü') { sb.Append('u'); prevDash = false; }
            else if (ch == 'ñ') { sb.Append('n'); prevDash = false; }
        }
        return sb.ToString().Trim('-');
    }
}
