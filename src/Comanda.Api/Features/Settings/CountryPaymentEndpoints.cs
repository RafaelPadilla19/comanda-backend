using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Infrastructure.Persistence;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Settings;

internal static class Countries
{
    public static readonly string[] Supported = ["SV", "GT", "HN", "NI", "CR", "PA"];
    public static bool IsValid(string c) => Supported.Contains(c);
}

// ==================== Restaurante: país ====================

public sealed class GetCountryEndpoint(IRepository<Tenant> tenants, ICurrentTenant current)
    : EndpointWithoutRequest<CountryDto>
{
    public override void Configure() => Get("/settings/country");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenant = current.TenantId is { } id ? await tenants.GetByIdAsync(id, ct) : null;
        await Send.OkAsync(new CountryDto { Country = tenant?.Country ?? string.Empty }, ct);
    }
}

public sealed class SetCountryEndpoint(IRepository<Tenant> tenants, ICurrentTenant current, ComandaDbContext db)
    : Endpoint<CountryDto, CountryDto>
{
    public override void Configure() => Put("/settings/country");

    public override async Task HandleAsync(CountryDto req, CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("tenant.no_encontrado", "Negocio no encontrado."), ct);
            return;
        }
        var code = (req.Country ?? string.Empty).Trim().ToUpperInvariant();
        if (!Countries.IsValid(code))
        {
            await HttpContext.SendErrorAsync(Error.Validation("pais.invalido", "Selecciona un país válido."), ct);
            return;
        }

        tenant.Country = code;
        tenants.Update(tenant);
        await db.SaveChangesAsync(ct);

        // Asegura que existan los métodos del nuevo país (no borra los ya configurados).
        await DbSeeder.SyncTenantPaymentMethodsAsync(db, code, ct);

        await Send.OkAsync(new CountryDto { Country = code }, ct);
    }
}

// ==================== Restaurante: métodos de pago (según su país) ====================

public sealed class ListPaymentMethodsEndpoint(
    IRepository<Tenant> tenants, ICurrentTenant current,
    IRepository<CountryPaymentMethod> catalog, IRepository<PaymentMethodConfig> methods)
    : EndpointWithoutRequest<List<PaymentMethodDto>>
{
    public override void Configure() => Get("/settings/payment-methods");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenant = current.TenantId is { } id ? await tenants.GetByIdAsync(id, ct) : null;
        var country = tenant?.Country ?? string.Empty;

        var available = (await catalog.ListAsync(c => c.Country == country && c.IsActive, ct))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Label).ToList();
        var enabled = (await methods.ListAsync(ct)).ToDictionary(m => m.Key, m => m.IsEnabled);

        var list = available.Select(c => new PaymentMethodDto
        {
            Key = c.Key, Label = c.Label, Description = c.Description, Emoji = c.Emoji, IsOnline = c.IsOnline,
            IsEnabled = enabled.TryGetValue(c.Key, out var on) ? on : c.DefaultEnabled,
        }).ToList();

        await Send.OkAsync(list, ct);
    }
}

public sealed class TogglePaymentMethodRequest
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public sealed class TogglePaymentMethodEndpoint(
    IRepository<Tenant> tenants, ICurrentTenant current, IPlanService planService,
    IRepository<CountryPaymentMethod> catalog, IRepository<PaymentMethodConfig> methods, IUnitOfWork uow)
    : Endpoint<TogglePaymentMethodRequest, PaymentMethodDto>
{
    public override void Configure() => Patch("/settings/payment-methods/{key}");

    public override async Task HandleAsync(TogglePaymentMethodRequest req, CancellationToken ct)
    {
        var tenant = current.TenantId is { } id ? await tenants.GetByIdAsync(id, ct) : null;
        var country = tenant?.Country ?? string.Empty;

        // El método debe existir en el catálogo del país del restaurante.
        if (await catalog.FirstOrDefaultAsync(c => c.Country == country && c.Key == req.Key && c.IsActive, ct) is not { } cat)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("pagos.no_encontrado", "Método de pago no disponible en tu país."), ct);
            return;
        }

        // Activar un método en línea requiere que el plan incluya Pagos en línea.
        if (req.IsEnabled && cat.IsOnline)
        {
            var feat = await planService.EnsureFeatureAsync(PlanFeature.OnlinePayments, ct);
            if (feat.IsFailure)
            {
                await HttpContext.SendErrorAsync(feat.Error, ct);
                return;
            }
        }

        var method = await methods.FirstOrDefaultAsync(m => m.Key == req.Key, ct);
        if (method is null)
        {
            method = new PaymentMethodConfig
            {
                Key = cat.Key, Label = cat.Label, Description = cat.Description, Emoji = cat.Emoji,
                SortOrder = cat.SortOrder, IsEnabled = req.IsEnabled,
            };
            await methods.AddAsync(method, ct);
        }
        else
        {
            method.IsEnabled = req.IsEnabled;
            methods.Update(method);
        }
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(new PaymentMethodDto
        {
            Key = method.Key, Label = method.Label, Description = method.Description, Emoji = method.Emoji,
            IsOnline = cat.IsOnline, IsEnabled = method.IsEnabled,
        }, ct);
    }
}

// ==================== Plataforma: catálogo de métodos por país ====================

public sealed class PlatformPaymentMethodsRequest { public string? Country { get; set; } }

public sealed class ListCountryPaymentMethodsEndpoint(ComandaDbContext db, IRMapper mapper)
    : Endpoint<PlatformPaymentMethodsRequest, List<CountryPaymentMethodDto>>
{
    public override void Configure() { Get("/platform/payment-methods"); Roles("PlatformAdmin"); }

    public override async Task HandleAsync(PlatformPaymentMethodsRequest req, CancellationToken ct)
    {
        var q = db.CountryPaymentMethods.AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Country))
        {
            var c = req.Country.Trim().ToUpperInvariant();
            q = q.Where(x => x.Country == c);
        }
        var list = await q.OrderBy(x => x.Country).ThenBy(x => x.SortOrder).ThenBy(x => x.Label).ToListAsync(ct);
        await Send.OkAsync(mapper.MapList<CountryPaymentMethod, CountryPaymentMethodDto>(list), ct);
    }
}

public sealed class CountryPaymentMethodUpsert
{
    public Guid? Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = "💳";
    public bool IsOnline { get; set; }
    public bool DefaultEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CountryPaymentMethodValidator : Validator<CountryPaymentMethodUpsert>
{
    public CountryPaymentMethodValidator()
    {
        RuleFor(x => x.Country).NotEmpty().Must(Countries.IsValid).WithMessage("País no soportado.");
        RuleFor(x => x.Key).NotEmpty().WithMessage("La clave es obligatoria.");
        RuleFor(x => x.Label).NotEmpty().WithMessage("El nombre es obligatorio.");
    }
}

public sealed class CreateCountryPaymentMethodEndpoint(ComandaDbContext db, IRMapper mapper)
    : Endpoint<CountryPaymentMethodUpsert, CountryPaymentMethodDto>
{
    public override void Configure() { Post("/platform/payment-methods"); Roles("PlatformAdmin"); }

    public override async Task HandleAsync(CountryPaymentMethodUpsert req, CancellationToken ct)
    {
        var country = req.Country.Trim().ToUpperInvariant();
        var key = req.Key.Trim();
        if (await db.CountryPaymentMethods.AnyAsync(x => x.Country == country && x.Key == key, ct))
        {
            await HttpContext.SendErrorAsync(Error.Conflict("pagos.duplicado", "Ya existe ese método en ese país."), ct);
            return;
        }
        var m = new CountryPaymentMethod { Country = country, Key = key };
        Apply(req, m);
        db.CountryPaymentMethods.Add(m);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<CountryPaymentMethod, CountryPaymentMethodDto>(m), ct);
    }

    internal static void Apply(CountryPaymentMethodUpsert req, CountryPaymentMethod m)
    {
        m.Label = req.Label.Trim();
        m.Description = req.Description.Trim();
        m.Emoji = string.IsNullOrWhiteSpace(req.Emoji) ? "💳" : req.Emoji.Trim();
        m.IsOnline = req.IsOnline;
        m.DefaultEnabled = req.DefaultEnabled;
        m.SortOrder = req.SortOrder;
        m.IsActive = req.IsActive;
    }
}

public sealed class UpdateCountryPaymentMethodEndpoint(ComandaDbContext db, IRMapper mapper)
    : Endpoint<CountryPaymentMethodUpsert, CountryPaymentMethodDto>
{
    public override void Configure() { Put("/platform/payment-methods/{id}"); Roles("PlatformAdmin"); }

    public override async Task HandleAsync(CountryPaymentMethodUpsert req, CancellationToken ct)
    {
        if (req.Id is not { } id || await db.CountryPaymentMethods.FirstOrDefaultAsync(x => x.Id == id, ct) is not { } m)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("pagos.no_encontrado", "Método no encontrado."), ct);
            return;
        }
        CreateCountryPaymentMethodEndpoint.Apply(req, m);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<CountryPaymentMethod, CountryPaymentMethodDto>(m), ct);
    }
}

public sealed class DeleteCountryPaymentMethodRequest { public Guid Id { get; set; } }

public sealed class DeleteCountryPaymentMethodEndpoint(ComandaDbContext db) : Endpoint<DeleteCountryPaymentMethodRequest>
{
    public override void Configure() { Delete("/platform/payment-methods/{id}"); Roles("PlatformAdmin"); }

    public override async Task HandleAsync(DeleteCountryPaymentMethodRequest req, CancellationToken ct)
    {
        if (await db.CountryPaymentMethods.FirstOrDefaultAsync(x => x.Id == req.Id, ct) is { } m)
        {
            db.CountryPaymentMethods.Remove(m);
            await db.SaveChangesAsync(ct);
        }
        await Send.NoContentAsync(ct);
    }
}
