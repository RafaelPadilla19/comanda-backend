using Comanda.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

/// <summary>Contexto del tenant actual, con alcance por petición (Scoped).</summary>
public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public bool HasTenant => TenantId.HasValue;
    public void Set(Guid tenantId) => TenantId = tenantId;
}

/// <summary>Resuelve el tenant en endpoints públicos (sin JWT) ignorando el filtro global.</summary>
public sealed class TenantResolver(ComandaDbContext db, ICurrentTenant current) : ITenantResolver
{
    public async Task<bool> ResolveByBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        var tenantId = await db.Branches.IgnoreQueryFilters()
            .Where(b => b.Id == branchId).Select(b => b.TenantId).FirstOrDefaultAsync(ct);
        if (tenantId == Guid.Empty) return false;
        current.Set(tenantId);
        return true;
    }

    public async Task<bool> ResolveBySlugAsync(string slug, CancellationToken ct = default)
    {
        var key = slug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Slug == key && t.IsActive).Select(t => new { t.Id }).FirstOrDefaultAsync(ct);
        if (tenant is null) return false;
        current.Set(tenant.Id);
        return true;
    }
}
