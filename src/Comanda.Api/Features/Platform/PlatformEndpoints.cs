using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Enums;
using Comanda.Infrastructure.Persistence;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Platform;

// ============================================================
//  Control-Plane / Super-Admin (operador del SaaS).
//  Vive por encima de los tenants: auth propia (rol PlatformAdmin),
//  consultas cross-tenant ignorando el filtro global.
// ============================================================

internal static class PlatformPolicy
{
    public const string Role = "PlatformAdmin";
}

// ---------------- Auth ----------------

public sealed class PlatformLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class PlatformLoginValidator : Validator<PlatformLoginRequest>
{
    public PlatformLoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Correo inválido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

public sealed class PlatformLoginEndpoint(ComandaDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    : Endpoint<PlatformLoginRequest, PlatformLoginResponse>
{
    public override void Configure()
    {
        Post("/platform/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlatformLoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var admin = await db.PlatformAdmins.FirstOrDefaultAsync(a => a.Email == email, ct);
        if (admin is null || !admin.IsActive || !hasher.Verify(req.Password, admin.PasswordHash))
        {
            await HttpContext.SendErrorAsync(Error.Unauthorized("platform.credenciales", "Credenciales incorrectas."), ct);
            return;
        }

        var (token, exp) = jwt.CreatePlatformToken(admin);
        await Send.OkAsync(new PlatformLoginResponse
        {
            Token = token, ExpiresAt = exp,
            Admin = new PlatformAdminDto { Id = admin.Id, Name = admin.Name, Email = admin.Email },
        }, ct);
    }
}

// ---------------- Planes ----------------

public sealed class PlatformPlansEndpoint(ComandaDbContext db, IRMapper mapper)
    : EndpointWithoutRequest<List<PlanDto>>
{
    public override void Configure() { Get("/platform/plans"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var plans = await db.Plans.OrderBy(p => p.SortOrder).ToListAsync(ct);
        await Send.OkAsync(mapper.MapList<Domain.Entities.Plan, PlanDto>(plans), ct);
    }
}

/// <summary>Alta o edición de un plan (todo configurable por el operador). Id nulo = crear.</summary>
public sealed class PlanUpsertRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public int MaxBranches { get; set; }
    public int MaxProducts { get; set; }
    public int MaxUsers { get; set; }
    public int MaxOrdersMonth { get; set; }
    public decimal OveragePrice { get; set; }
    public int RetentionMonths { get; set; }
    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PlanUpsertValidator : Validator<PlanUpsertRequest>
{
    public PlanUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del plan es obligatorio.");
        RuleFor(x => x.PriceMonthly).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
        RuleFor(x => x.OveragePrice).GreaterThanOrEqualTo(0).WithMessage("El overage no puede ser negativo.");
    }
}

public sealed class CreatePlanEndpoint(ComandaDbContext db, IRMapper mapper) : Endpoint<PlanUpsertRequest, PlanDto>
{
    public override void Configure() { Post("/platform/plans"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(PlanUpsertRequest req, CancellationToken ct)
    {
        var plan = new Domain.Entities.Plan();
        Apply(req, plan);
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Domain.Entities.Plan, PlanDto>(plan), ct);
    }

    internal static void Apply(PlanUpsertRequest req, Domain.Entities.Plan p)
    {
        p.Name = req.Name.Trim();
        p.PriceMonthly = req.PriceMonthly;
        p.MaxBranches = Math.Max(0, req.MaxBranches);
        p.MaxProducts = Math.Max(0, req.MaxProducts);
        p.MaxUsers = Math.Max(0, req.MaxUsers);
        p.MaxOrdersMonth = Math.Max(0, req.MaxOrdersMonth);
        p.OveragePrice = req.OveragePrice;
        p.RetentionMonths = Math.Max(0, req.RetentionMonths);
        p.OnlinePayments = req.OnlinePayments;
        p.Coupons = req.Coupons;
        p.Loyalty = req.Loyalty;
        p.AdvancedReports = req.AdvancedReports;
        p.Inventory = req.Inventory;
        p.SortOrder = req.SortOrder;
        p.IsActive = req.IsActive;
    }
}

public sealed class UpdatePlanEndpoint(ComandaDbContext db, IRMapper mapper) : Endpoint<PlanUpsertRequest, PlanDto>
{
    public override void Configure() { Put("/platform/plans/{id}"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(PlanUpsertRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct) is not { } plan)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.plan", "Plan no encontrado."), ct);
            return;
        }
        CreatePlanEndpoint.Apply(req, plan);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Domain.Entities.Plan, PlanDto>(plan), ct);
    }
}

public sealed class DeletePlanRequest { public Guid Id { get; set; } }

public sealed class DeletePlanEndpoint(ComandaDbContext db) : Endpoint<DeletePlanRequest>
{
    public override void Configure() { Delete("/platform/plans/{id}"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(DeletePlanRequest req, CancellationToken ct)
    {
        if (await db.Plans.FirstOrDefaultAsync(p => p.Id == req.Id, ct) is not { } plan)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.plan", "Plan no encontrado."), ct);
            return;
        }
        // No se puede borrar un plan en uso: se desactiva en su lugar (deja de ofrecerse).
        if (await db.Tenants.AnyAsync(t => t.PlanId == req.Id, ct))
        {
            plan.IsActive = false;
            await db.SaveChangesAsync(ct);
            await Send.OkAsync(new { deactivated = true }, ct);
            return;
        }
        db.Plans.Remove(plan);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

// ---------------- Ajustes del operador: credenciales Wompi de plataforma ----------------

public sealed class GetPlatformWompiEndpoint(ComandaDbContext db) : EndpointWithoutRequest<WompiConfigDto>
{
    public override void Configure() { Get("/platform/settings/wompi"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var s = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        await Send.OkAsync(new WompiConfigDto { AppId = s?.WompiAppId ?? string.Empty, Connected = s?.HasWompi ?? false }, ct);
    }
}

public sealed class SavePlatformWompiRequest
{
    public string AppId { get; set; } = string.Empty;
    /// <summary>Vacío = conservar el secret actual; con valor = reemplazarlo. Para desconectar, vaciar AppId.</summary>
    public string ApiSecret { get; set; } = string.Empty;
}

public sealed class SavePlatformWompiEndpoint(ComandaDbContext db, ISecretProtector secrets)
    : Endpoint<SavePlatformWompiRequest, WompiConfigDto>
{
    public override void Configure() { Put("/platform/settings/wompi"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(SavePlatformWompiRequest req, CancellationToken ct)
    {
        var s = await db.PlatformSettings.FirstOrDefaultAsync(ct);
        if (s is null) { s = new Domain.Entities.PlatformSetting(); db.PlatformSettings.Add(s); }

        var appId = req.AppId.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            s.WompiAppId = string.Empty;
            s.WompiApiSecretEnc = string.Empty;
        }
        else
        {
            s.WompiAppId = appId;
            if (!string.IsNullOrWhiteSpace(req.ApiSecret))
                s.WompiApiSecretEnc = secrets.Protect(req.ApiSecret.Trim());
        }
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(new WompiConfigDto { AppId = s.WompiAppId, Connected = s.HasWompi }, ct);
    }
}

// ---------------- Tenants ----------------

public sealed class PlatformTenantsEndpoint(ComandaDbContext db) : EndpointWithoutRequest<List<PlatformTenantDto>>
{
    public override void Configure() { Get("/platform/tenants"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants.Include(t => t.Plan).OrderBy(t => t.Name).ToListAsync(ct);

        // Conteos por tenant (cross-tenant → ignorar el filtro global).
        var branches = await db.Branches.IgnoreQueryFilters().GroupBy(x => x.TenantId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var products = await db.Products.IgnoreQueryFilters().GroupBy(x => x.TenantId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var users = await db.Users.IgnoreQueryFilters().GroupBy(x => x.TenantId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var orders = await db.Orders.IgnoreQueryFilters().GroupBy(x => x.TenantId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);

        int Get(Dictionary<Guid, int> d, Guid id) => d.TryGetValue(id, out var c) ? c : 0;

        var list = tenants.Select(t => new PlatformTenantDto
        {
            Id = t.Id, Name = t.Name, Slug = t.Slug, IsActive = t.IsActive,
            SubscriptionStatus = t.SubscriptionStatus,
            PlanId = t.PlanId, PlanName = t.Plan?.Name ?? "Sin plan", PriceMonthly = t.Plan?.PriceMonthly ?? 0,
            Branches = Get(branches, t.Id), Products = Get(products, t.Id),
            Users = Get(users, t.Id), Orders = Get(orders, t.Id),
            CreatedAt = t.CreatedAt,
        }).ToList();

        await Send.OkAsync(list, ct);
    }
}

public sealed class AssignPlanRequest
{
    public Guid Id { get; set; }          // tenant (ruta)
    public Guid PlanId { get; set; }
}

public sealed class AssignPlanEndpoint(ComandaDbContext db) : Endpoint<AssignPlanRequest>
{
    public override void Configure() { Put("/platform/tenants/{id}/plan"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(AssignPlanRequest req, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
        if (tenant is null || plan is null)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.no_encontrado", "Tenant o plan no encontrado."), ct);
            return;
        }
        tenant.PlanId = plan.Id;
        if (tenant.SubscriptionStatus is SubscriptionStatus.Trial) tenant.SubscriptionStatus = SubscriptionStatus.Active;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class SetTenantStatusRequest
{
    public Guid Id { get; set; }
    public SubscriptionStatus Status { get; set; }
}

public sealed class SetTenantStatusEndpoint(ComandaDbContext db) : Endpoint<SetTenantStatusRequest>
{
    public override void Configure() { Put("/platform/tenants/{id}/status"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(SetTenantStatusRequest req, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        if (tenant is null)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.tenant", "Tenant no encontrado."), ct);
            return;
        }
        tenant.SubscriptionStatus = req.Status;
        tenant.IsActive = req.Status != SubscriptionStatus.Suspended && req.Status != SubscriptionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

// ---------------- Métricas ----------------

public sealed class PlatformMetricsEndpoint(ComandaDbContext db) : EndpointWithoutRequest<PlatformMetricsDto>
{
    public override void Configure() { Get("/platform/metrics"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenants = await db.Tenants.Include(t => t.Plan).ToListAsync(ct);
        await Send.OkAsync(new PlatformMetricsDto
        {
            TotalTenants = tenants.Count,
            ActiveTenants = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Active),
            TrialTenants = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Trial),
            SuspendedTenants = tenants.Count(t => t.SubscriptionStatus == SubscriptionStatus.Suspended),
            Mrr = tenants.Where(t => t.SubscriptionStatus == SubscriptionStatus.Active && t.Plan != null).Sum(t => t.Plan!.PriceMonthly),
            TotalOrders = await db.Orders.IgnoreQueryFilters().CountAsync(ct),
            TotalCustomers = await db.Customers.IgnoreQueryFilters().CountAsync(ct),
        }, ct);
    }
}
