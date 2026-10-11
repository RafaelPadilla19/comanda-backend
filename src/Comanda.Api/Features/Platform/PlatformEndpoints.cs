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

// ---------------- Países (catálogo global, editable sin redeploy) ----------------

public sealed class PlatformCountriesEndpoint(ComandaDbContext db) : EndpointWithoutRequest<List<PlatformCountryDto>>
{
    public override void Configure() { Get("/platform/countries"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countries = await db.Countries.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var tenantCounts = await db.Tenants.IgnoreQueryFilters()
            .GroupBy(t => t.Country).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);

        var list = countries.Select(c => new PlatformCountryDto
        {
            Id = c.Id, Code = c.Code, Name = c.Name, Flag = c.Flag,
            Currency = c.Currency, CurrencySymbol = c.CurrencySymbol, TaxRate = c.TaxRate, TaxLabel = c.TaxLabel,
            IsActive = c.IsActive, SortOrder = c.SortOrder,
            TenantsCount = tenantCounts.TryGetValue(c.Code, out var n) ? n : 0,
        }).ToList();

        await Send.OkAsync(list, ct);
    }
}

public sealed class CountryUpsertRequest
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    /// <summary>Porcentaje (13 = 13%), no fracción — se convierte al guardar.</summary>
    public decimal TaxPercent { get; set; }
    public string TaxLabel { get; set; } = "IVA";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CountryUpsertValidator : Validator<CountryUpsertRequest>
{
    public CountryUpsertValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(2).WithMessage("El código debe ser ISO-2 (p.ej. SV, CO).");
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del país es obligatorio.");
        RuleFor(x => x.Currency).NotEmpty().Length(3).WithMessage("La moneda debe ser un código ISO de 3 letras.");
        RuleFor(x => x.CurrencySymbol).NotEmpty().WithMessage("El símbolo de moneda es obligatorio.");
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 50).WithMessage("El impuesto debe estar entre 0% y 50%.");
    }
}

public sealed class CreateCountryEndpoint(ComandaDbContext db) : Endpoint<CountryUpsertRequest, PlatformCountryDto>
{
    public override void Configure() { Post("/platform/countries"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CountryUpsertRequest req, CancellationToken ct)
    {
        var code = req.Code.Trim().ToUpperInvariant();
        if (await db.Countries.AnyAsync(c => c.Code == code, ct))
        {
            await HttpContext.SendErrorAsync(Error.Conflict("paises.duplicado", "Ese país ya está en el catálogo."), ct);
            return;
        }
        var c = new Domain.Entities.Country { Code = code };
        Apply(req, c);
        db.Countries.Add(c);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ToDto(c), ct);
    }

    internal static void Apply(CountryUpsertRequest req, Domain.Entities.Country c)
    {
        c.Name = req.Name.Trim();
        c.Flag = req.Flag.Trim();
        c.Currency = req.Currency.Trim().ToUpperInvariant();
        c.CurrencySymbol = req.CurrencySymbol.Trim();
        c.TaxRate = req.TaxPercent / 100m;
        c.TaxLabel = string.IsNullOrWhiteSpace(req.TaxLabel) ? "IVA" : req.TaxLabel.Trim();
        c.SortOrder = req.SortOrder;
        c.IsActive = req.IsActive;
    }

    internal static PlatformCountryDto ToDto(Domain.Entities.Country c) => new()
    {
        Id = c.Id, Code = c.Code, Name = c.Name, Flag = c.Flag,
        Currency = c.Currency, CurrencySymbol = c.CurrencySymbol, TaxRate = c.TaxRate, TaxLabel = c.TaxLabel,
        IsActive = c.IsActive, SortOrder = c.SortOrder,
    };
}

public sealed class UpdateCountryEndpoint(ComandaDbContext db) : Endpoint<CountryUpsertRequest, PlatformCountryDto>
{
    public override void Configure() { Put("/platform/countries/{id}"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(CountryUpsertRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await db.Countries.FirstOrDefaultAsync(c => c.Id == id, ct) is not { } c)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("paises.no_encontrado", "País no encontrado."), ct);
            return;
        }
        // El código no se edita: cambiarlo rompería los tenants que ya lo usan (Tenant.Country es texto, no FK).
        CreateCountryEndpoint.Apply(req, c);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(CreateCountryEndpoint.ToDto(c), ct);
    }
}

public sealed class DeleteCountryRequest { public Guid Id { get; set; } }

public sealed class DeleteCountryEndpoint(ComandaDbContext db) : Endpoint<DeleteCountryRequest>
{
    public override void Configure() { Delete("/platform/countries/{id}"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(DeleteCountryRequest req, CancellationToken ct)
    {
        if (await db.Countries.FirstOrDefaultAsync(c => c.Id == req.Id, ct) is not { } c)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("paises.no_encontrado", "País no encontrado."), ct);
            return;
        }
        // Si ya hay tenants en ese país, no se borra (perderían su catálogo de moneda/impuesto): se desactiva.
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Country == c.Code, ct))
        {
            c.IsActive = false;
            await db.SaveChangesAsync(ct);
            await Send.OkAsync(new { deactivated = true }, ct);
            return;
        }
        db.Countries.Remove(c);
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
            Id = t.Id, Name = t.Name, Slug = t.Slug, Country = t.Country, IsActive = t.IsActive,
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

public sealed class ExtendSubscriptionValidator : Validator<ExtendSubscriptionRequest>
{
    public ExtendSubscriptionValidator()
    {
        RuleFor(x => x.SubscriptionEndsAt).GreaterThan(DateTime.UtcNow)
            .WithMessage("La nueva fecha debe ser en el futuro.");
    }
}

/// <summary>Extiende manualmente la suscripción de un tenant (cortesía, pago fuera de banda,
/// negociación directa). No crea un SubscriptionPayment: es un ajuste administrativo, no un cobro.</summary>
public sealed class ExtendSubscriptionEndpoint(ComandaDbContext db) : Endpoint<ExtendSubscriptionRequest>
{
    public override void Configure() { Put("/platform/tenants/{id}/subscription"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(ExtendSubscriptionRequest req, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        if (tenant is null)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.tenant", "Tenant no encontrado."), ct);
            return;
        }
        tenant.SubscriptionEndsAt = req.SubscriptionEndsAt;
        if (tenant.SubscriptionStatus is SubscriptionStatus.PastDue or SubscriptionStatus.Trial)
            tenant.SubscriptionStatus = SubscriptionStatus.Active;
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

// ---------------- Ficha de tenant: detalle + analítica ----------------

public sealed class TenantIdRequest { public Guid Id { get; set; } }

public sealed class PlatformTenantDetailEndpoint(ComandaDbContext db)
    : Endpoint<TenantIdRequest, PlatformTenantDetailDto>
{
    public override void Configure() { Get("/platform/tenants/{id}"); Roles(PlatformPolicy.Role); }

    public override async Task HandleAsync(TenantIdRequest req, CancellationToken ct)
    {
        var tenant = await db.Tenants.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        if (tenant is null)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("platform.tenant", "Tenant no encontrado."), ct);
            return;
        }

        var branches = await db.Branches.IgnoreQueryFilters().Where(b => b.TenantId == tenant.Id)
            .OrderBy(b => b.Name).ToListAsync(ct);
        var users = await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenant.Id)
            .OrderBy(u => u.Name).ToListAsync(ct);
        var productsCount = await db.Products.IgnoreQueryFilters().CountAsync(p => p.TenantId == tenant.Id, ct);
        var customersCount = await db.Customers.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenant.Id, ct);
        var orders = await db.Orders.IgnoreQueryFilters().Include(o => o.Items)
            .Where(o => o.TenantId == tenant.Id).ToListAsync(ct);
        var payments = await db.SubscriptionPayments.IgnoreQueryFilters().Where(p => p.TenantId == tenant.Id)
            .OrderByDescending(p => p.CreatedAt).Take(10).ToListAsync(ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ordersThisMonth = orders.Where(o => o.CreatedAt >= monthStart).ToList();
        var revenueTotal = orders.Sum(o => o.Total);

        await Send.OkAsync(new PlatformTenantDetailDto
        {
            Id = tenant.Id, Name = tenant.Name, Slug = tenant.Slug, Country = tenant.Country,
            Currency = tenant.Currency, CurrencySymbol = tenant.CurrencySymbol, TaxRate = tenant.TaxRate,
            IsActive = tenant.IsActive, SubscriptionStatus = tenant.SubscriptionStatus,
            TrialEndsAt = tenant.TrialEndsAt, SubscriptionEndsAt = tenant.SubscriptionEndsAt,
            PlanId = tenant.PlanId, PlanName = tenant.Plan?.Name ?? "Sin plan", PriceMonthly = tenant.Plan?.PriceMonthly ?? 0,
            CreatedAt = tenant.CreatedAt,

            BranchesCount = branches.Count, ProductsCount = productsCount, UsersCount = users.Count,
            CustomersCount = customersCount,
            TotalOrders = orders.Count, OrdersThisMonth = ordersThisMonth.Count,
            RevenueTotal = revenueTotal, RevenueThisMonth = ordersThisMonth.Sum(o => o.Total),
            AvgTicket = orders.Count > 0 ? revenueTotal / orders.Count : 0,
            LastOrderAt = orders.Count > 0 ? orders.Max(o => o.CreatedAt) : null,

            BranchList = branches.Select(b => new PlatformTenantBranchDto
                { Name = b.Name, Address = b.Address, IsActive = b.IsActive }).ToList(),
            UserList = users.Select(u => new PlatformTenantUserDto
                { Name = u.Name, Email = u.Email, Role = u.Role.ToString(), IsActive = u.IsActive }).ToList(),
            TopProducts = Comanda.Api.Features.Reports.ReportHelpers.TopProducts(orders, 5),
            Payments = payments.Select(p => new SubscriptionPaymentDto
            {
                Amount = p.Amount, PlanName = p.PlanName, IsPaid = p.IsPaid, CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt, PeriodEndsAt = p.PeriodEndsAt, PeriodMonths = p.PeriodMonths,
            }).ToList(),
        }, ct);
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
