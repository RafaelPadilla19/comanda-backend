using Comanda.Domain.Common;
using Comanda.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Comanda.Infrastructure.Persistence;

/// <summary>
/// Lógica de los trabajos de mantenimiento, compartida entre los BackgroundServices
/// (VPS / desarrollo, proceso siempre vivo) y los endpoints /jobs/* (Cloud Run con
/// escala a cero, disparados por Cloud Scheduler). Idempotente: correr dos veces no daña.
/// </summary>
public static class MaintenanceJobs
{
    /// <summary>Purga pedidos más antiguos que la retención del plan de cada tenant. Devuelve cuántos borró.</summary>
    public static async Task<int> PurgeRetentionAsync(ComandaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var tenants = await db.Tenants.Include(t => t.Plan)
            .Where(t => t.Plan != null && t.Plan.RetentionMonths > 0)
            .Select(t => new { t.Id, t.Name, Months = t.Plan!.RetentionMonths })
            .ToListAsync(ct);

        var total = 0;
        foreach (var t in tenants)
        {
            var cutoff = DateTime.UtcNow.AddMonths(-t.Months);
            var deleted = await db.Orders.IgnoreQueryFilters()
                .Where(o => o.TenantId == t.Id && o.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            total += deleted;
            if (deleted > 0)
                logger.LogInformation("Retención: {Count} pedidos purgados de «{Tenant}» (anteriores a {Cutoff:yyyy-MM-dd}).",
                    deleted, t.Name, cutoff);
        }
        return total;
    }

    /// <summary>Pasa a PastDue los tenants de plan pago vencidos más allá de la gracia. Devuelve cuántos afectó.</summary>
    public static async Task<int> SweepSubscriptionsAsync(ComandaDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-SubscriptionPolicy.GraceDays);

        var overdue = await db.Tenants.IgnoreQueryFilters().Include(t => t.Plan)
            .Where(t => t.Plan != null && t.Plan.PriceMonthly > 0
                && t.SubscriptionStatus == SubscriptionStatus.Active
                && t.SubscriptionEndsAt != null && t.SubscriptionEndsAt < cutoff)
            .ToListAsync(ct);

        foreach (var t in overdue)
        {
            t.SubscriptionStatus = SubscriptionStatus.PastDue;
            logger.LogInformation("Suscripción vencida: «{Tenant}» pasó a PastDue (venció {End:yyyy-MM-dd}).",
                t.Name, t.SubscriptionEndsAt);
        }
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);
        return overdue.Count;
    }
}
