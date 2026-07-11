using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

/// <summary>
/// Aplica límites y gating del plan del tenant actual. Los conteos pasan por el filtro
/// global de tenant del DbContext (no hace falta filtrar por TenantId a mano).
/// </summary>
public sealed class PlanService(ComandaDbContext db, ICurrentTenant current) : IPlanService
{
    private async Task<Plan?> CurrentPlanAsync(CancellationToken ct)
    {
        if (current.TenantId is not { } id) return null;
        var tenant = await db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == id, ct);
        return tenant?.Plan;
    }

    private async Task<Tenant?> CurrentTenantAsync(CancellationToken ct)
    {
        if (current.TenantId is not { } id) return null;
        return await db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    private static DateTime MonthStartUtc()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Clave del ciclo de overage (año*100+mes). Al cambiar el mes, lo facturado se reinicia solo.</summary>
    private static int CycleKey(DateTime now) => now.Year * 100 + now.Month;

    public async Task<Result> EnsureOrderAllowedAsync(CancellationToken ct = default)
    {
        var tenant = await CurrentTenantAsync(ct);
        var plan = tenant?.Plan;
        if (plan is null || plan.MaxOrdersMonth <= 0) return Result.Success();

        var since = MonthStartUtc();
        var count = await db.Orders.CountAsync(o => o.CreatedAt >= since, ct);
        if (count < plan.MaxOrdersMonth) return Result.Success();

        // Llegó al tope: solo se permite seguir si el dueño habilitó pedidos extra Y el plan los cobra.
        if (tenant!.AllowOverage && plan.OveragePrice > 0) return Result.Success();

        return Result.Failure(Error.Validation("plan.limite_pedidos",
            $"Alcanzaste el límite de {plan.MaxOrdersMonth} pedidos de tu plan «{plan.Name}» este mes. " +
            "Mejora tu plan o activa los pedidos extra desde Suscripción para seguir recibiendo pedidos."));
    }

    public async Task<Result> EnsureFeatureAsync(PlanFeature feature, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan is null || HasFeature(plan, feature)) return Result.Success();

        return Result.Failure(Error.Forbidden("plan.funcion_no_incluida",
            $"La función «{FeatureLabel(feature)}» no está incluida en tu plan «{plan.Name}». Mejora tu plan para activarla."));
    }

    public async Task<bool> HasFeatureAsync(PlanFeature feature, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        return plan is null || HasFeature(plan, feature);
    }

    public async Task<Result> EnsureCanAddBranchAsync(CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan is null || plan.MaxBranches <= 0) return Result.Success();
        var count = await db.Branches.CountAsync(ct);
        return count < plan.MaxBranches
            ? Result.Success()
            : Result.Failure(Error.Validation("plan.limite_sucursales",
                $"Tu plan «{plan.Name}» permite {plan.MaxBranches} sucursal(es). Mejora tu plan para agregar más."));
    }

    public async Task<Result> EnsureCanAddProductAsync(CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan is null || plan.MaxProducts <= 0) return Result.Success();
        var count = await db.Products.CountAsync(ct);
        return count < plan.MaxProducts
            ? Result.Success()
            : Result.Failure(Error.Validation("plan.limite_productos",
                $"Tu plan «{plan.Name}» permite {plan.MaxProducts} productos. Mejora tu plan para agregar más."));
    }

    public async Task<Result> EnsureCanAddUserAsync(CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan is null || plan.MaxUsers <= 0) return Result.Success();
        var count = await db.Users.CountAsync(ct);
        return count < plan.MaxUsers
            ? Result.Success()
            : Result.Failure(Error.Validation("plan.limite_usuarios",
                $"Tu plan «{plan.Name}» permite {plan.MaxUsers} usuarios. Mejora tu plan para agregar más."));
    }

    public async Task<PlanUsage> GetUsageAsync(CancellationToken ct = default)
    {
        var tenant = await CurrentTenantAsync(ct);
        var plan = tenant?.Plan;
        var since = MonthStartUtc();
        var ordersMonth = await db.Orders.CountAsync(o => o.CreatedAt >= since, ct);
        var branches = await db.Branches.CountAsync(ct);
        var products = await db.Products.CountAsync(ct);
        var users = await db.Users.CountAsync(ct);

        if (plan is null)
            return new PlanUsage { HasPlan = false, OrdersThisMonth = ordersMonth, Branches = branches, Products = products, Users = users };

        // Overage mostrado = el PENDIENTE de cobrar (descontando lo ya facturado en este ciclo).
        var overageGross = plan.MaxOrdersMonth > 0 ? Math.Max(0, ordersMonth - plan.MaxOrdersMonth) : 0;
        var billed = tenant!.OverageCycleKey == CycleKey(DateTime.UtcNow) ? tenant.OverageBilledOrders : 0;
        var overagePending = Math.Max(0, overageGross - billed);
        return new PlanUsage
        {
            HasPlan = true,
            PlanName = plan.Name,
            OrdersThisMonth = ordersMonth,
            MaxOrdersMonth = plan.MaxOrdersMonth,
            OveragePrice = plan.OveragePrice,
            OverageOrders = overagePending,
            OverageAmount = overagePending * plan.OveragePrice,
            OverageAvailable = plan.OveragePrice > 0,
            AllowOverage = tenant.AllowOverage,
            Branches = branches, MaxBranches = plan.MaxBranches,
            Products = products, MaxProducts = plan.MaxProducts,
            Users = users, MaxUsers = plan.MaxUsers,
            RetentionMonths = plan.RetentionMonths,
            OnlinePayments = plan.OnlinePayments,
            Coupons = plan.Coupons,
            Loyalty = plan.Loyalty,
            AdvancedReports = plan.AdvancedReports,
            Inventory = plan.Inventory,
        };
    }

    public async Task<OverageBill> GetUnbilledOverageAsync(CancellationToken ct = default)
    {
        var cycle = CycleKey(DateTime.UtcNow);
        var tenant = await CurrentTenantAsync(ct);
        var plan = tenant?.Plan;
        if (plan is null || plan.MaxOrdersMonth <= 0 || plan.OveragePrice <= 0)
            return new OverageBill { CycleKey = cycle };

        var since = MonthStartUtc();
        var ordersMonth = await db.Orders.CountAsync(o => o.CreatedAt >= since, ct);
        var overageGross = Math.Max(0, ordersMonth - plan.MaxOrdersMonth);
        var billed = tenant!.OverageCycleKey == cycle ? tenant.OverageBilledOrders : 0;
        var unbilled = Math.Max(0, overageGross - billed);
        return new OverageBill { Orders = unbilled, Amount = unbilled * plan.OveragePrice, CycleKey = cycle };
    }

    private static bool HasFeature(Plan p, PlanFeature f) => f switch
    {
        PlanFeature.OnlinePayments => p.OnlinePayments,
        PlanFeature.Coupons => p.Coupons,
        PlanFeature.Loyalty => p.Loyalty,
        PlanFeature.AdvancedReports => p.AdvancedReports,
        PlanFeature.Inventory => p.Inventory,
        _ => false,
    };

    private static string FeatureLabel(PlanFeature f) => f switch
    {
        PlanFeature.OnlinePayments => "Pagos en línea",
        PlanFeature.Coupons => "Cupones y promociones",
        PlanFeature.Loyalty => "Fidelización",
        PlanFeature.AdvancedReports => "Reportes avanzados",
        PlanFeature.Inventory => "Inventario",
        _ => f.ToString(),
    };
}
