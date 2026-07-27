using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using FastEndpoints;
using FluentValidation;

namespace Comanda.Api.Features.Billing;

/// <summary>Estado de la suscripción del restaurante actual + consumo del plan + historial de pagos.</summary>
public sealed class GetBillingEndpoint(
    IRepository<Tenant> tenants, IRepository<Plan> plans, IRepository<SubscriptionPayment> payments,
    ICurrentTenant current, IPlanService planService, RMapper.Core.Interfaces.IRMapper mapper)
    : EndpointWithoutRequest<BillingDto>
{
    public override void Configure() => Get("/billing");

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await Send.OkAsync(new BillingDto(), ct);
            return;
        }

        var plan = tenant.PlanId is { } pid ? await plans.GetByIdAsync(pid, ct) : null;
        var history = (await payments.ListAsync(p => p.TenantId == id, ct))
            .OrderByDescending(p => p.CreatedAt).Take(10)
            .Select(p => new SubscriptionPaymentDto
            {
                Amount = p.Amount, PlanName = p.PlanName, IsPaid = p.IsPaid, CreatedAt = p.CreatedAt, PaidAt = p.PaidAt,
                PeriodEndsAt = p.PeriodEndsAt, PeriodMonths = p.PeriodMonths,
            }).ToList();

        var usage = await planService.GetUsageAsync(ct);

        await Send.OkAsync(new BillingDto
        {
            HasPlan = plan is not null,
            PlanName = plan?.Name ?? "Sin plan",
            PriceMonthly = plan?.PriceMonthly ?? 0,
            Status = tenant.SubscriptionStatus,
            SubscriptionEndsAt = tenant.SubscriptionEndsAt,
            Payments = history,
            Usage = mapper.Map<Domain.Abstractions.PlanUsage, PlanUsageDto>(usage),
        }, ct);
    }
}

/// <summary>Lista los planes activos para que el restaurante pueda comparar y mejorar.</summary>
public sealed class GetBillingPlansEndpoint(IRepository<Plan> plans, RMapper.Core.Interfaces.IRMapper mapper)
    : EndpointWithoutRequest<List<PlanDto>>
{
    public override void Configure() => Get("/billing/plans");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = (await plans.ListAsync(p => p.IsActive, ct)).OrderBy(p => p.SortOrder).ThenBy(p => p.PriceMonthly).ToList();
        await Send.OkAsync(mapper.MapList<Plan, PlanDto>(list), ct);
    }
}

public sealed class SubscriptionCheckoutRequest
{
    public string ReturnUrl { get; set; } = string.Empty;
    /// <summary>Plan a contratar; si va vacío, se cobra/renueva el plan actual.</summary>
    public Guid? PlanId { get; set; }
}

public sealed class SubscriptionCheckoutValidator : Validator<SubscriptionCheckoutRequest>
{
    public SubscriptionCheckoutValidator()
        => RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage("Falta la URL de retorno.");
}

/// <summary>Inicia el cobro de la suscripción del plan actual (restaurante → Innovacors).</summary>
public sealed class SubscriptionCheckoutEndpoint(
    IRepository<Tenant> tenants, IRepository<Plan> plans, IRepository<SubscriptionPayment> payments,
    IRepository<PlatformSetting> platformSettings, ISecretProtector secrets,
    ICurrentTenant current, IPaymentsClient paymentsClient, IPlanService planService, IUnitOfWork uow)
    : Endpoint<SubscriptionCheckoutRequest, SubscriptionCheckoutResponse>
{
    public override void Configure() => Post("/billing/checkout");

    public override async Task HandleAsync(SubscriptionCheckoutRequest req, CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("tenant.no_encontrado", "Negocio no encontrado."), ct);
            return;
        }

        // Plan objetivo: el elegido (mejora/cambio) o el actual (renovación).
        var targetId = req.PlanId ?? tenant.PlanId;
        if (targetId is not { } pid || await plans.GetByIdAsync(pid, ct) is not { IsActive: true } plan)
        {
            await HttpContext.SendErrorAsync(Error.Validation("suscripcion.sin_plan", "Selecciona un plan válido."), ct);
            return;
        }

        // Plan gratuito: se aplica y activa de inmediato, sin cobro.
        if (plan.PriceMonthly <= 0)
        {
            var baseDate = tenant.SubscriptionEndsAt is { } e && e > DateTime.UtcNow ? e : DateTime.UtcNow;
            tenant.PlanId = plan.Id;
            tenant.SubscriptionEndsAt = baseDate.AddMonths(1);
            tenant.SubscriptionStatus = SubscriptionStatus.Active;
            tenants.Update(tenant);
            await uow.SaveChangesAsync(ct);
            await Send.OkAsync(new SubscriptionCheckoutResponse { Paid = true, Message = $"Plan {plan.Name} activado." }, ct);
            return;
        }

        // Overage acumulado del ciclo (solo si el plan que se cobra es el actual; en una mejora a otro
        // plan no arrastramos el excedente del plan viejo). Se suma al monto del cobro.
        var overage = plan.Id == tenant.PlanId
            ? await planService.GetUnbilledOverageAsync(ct)
            : new OverageBill { CycleKey = 0 };
        var amount = plan.PriceMonthly + overage.Amount;

        // Idempotencia: si ya existe un cobro pendiente (sin pagar) para este mismo plan,
        // lo reutilizamos en vez de crear otro. Evita duplicados cuando el usuario reintenta
        // "Pagar" (p.ej. el popup del navegador bloqueó la ventana la primera vez).
        var pending = (await payments.ListAsync(p => p.TenantId == id && p.PlanId == plan.Id && !p.IsPaid, ct))
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        SubscriptionPayment sub;
        if (pending is not null)
        {
            pending.Amount = amount;
            pending.OverageAmount = overage.Amount;
            pending.OverageOrders = overage.Orders;
            pending.OverageCycleKey = overage.CycleKey;
            sub = pending;
            payments.Update(sub);
        }
        else
        {
            sub = new SubscriptionPayment
            {
                TenantId = id, PlanId = plan.Id, PlanName = plan.Name, Amount = amount, PeriodMonths = 1,
                OverageAmount = overage.Amount, OverageOrders = overage.Orders, OverageCycleKey = overage.CycleKey,
            };
            await payments.AddAsync(sub, ct);
        }
        await uow.SaveChangesAsync(ct);

        // Llaves Wompi de plataforma (configuradas por el operador en /platform). El secret va cifrado.
        var settings = (await platformSettings.ListAsync(ct)).FirstOrDefault();
        var platformAppId = settings?.WompiAppId;
        var platformSecret = settings is { } s && !string.IsNullOrEmpty(s.WompiApiSecretEnc)
            ? secrets.Unprotect(s.WompiApiSecretEnc) : null;

        var desc = overage.Orders > 0
            ? $"Suscripción {plan.Name} + {overage.Orders} pedido(s) extra · {tenant.Name}"
            : $"Suscripción {plan.Name} · {tenant.Name}";
        var charge = await paymentsClient.CreateSubscriptionChargeAsync(
            amount, desc, sub.Id.ToString(), id.ToString(),
            req.ReturnUrl, platformAppId, platformSecret, ct);

        if (charge is null)
        {
            await HttpContext.SendErrorAsync(Error.Failure("suscripcion.cobro", "No se pudo iniciar el cobro. Intenta de nuevo."), ct);
            return;
        }

        sub.PaymentRef = charge.ChargeId.ToString();
        payments.Update(sub);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(new SubscriptionCheckoutResponse { Paid = false, PaymentUrl = charge.PayUrl }, ct);
    }
}

public sealed class SetOverageRequest
{
    /// <summary>true = habilitar pedidos extra sobre el límite del plan (se cobra el excedente).</summary>
    public bool Allow { get; set; }
}

/// <summary>Activa o desactiva los pedidos extra (overage) del restaurante. Apagado por defecto.</summary>
public sealed class SetOverageEndpoint(
    IRepository<Tenant> tenants, IRepository<Plan> plans, ICurrentTenant current, IPlanService planService,
    IUnitOfWork uow, RMapper.Core.Interfaces.IRMapper mapper)
    : Endpoint<SetOverageRequest, PlanUsageDto>
{
    public override void Configure() => Put("/billing/overage");

    public override async Task HandleAsync(SetOverageRequest req, CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("tenant.no_encontrado", "Negocio no encontrado."), ct);
            return;
        }

        // Solo se puede activar si el plan define un precio de overage; si no, no hay nada que cobrar.
        var plan = tenant.PlanId is { } pid ? await plans.GetByIdAsync(pid, ct) : null;
        if (req.Allow && (plan is null || plan.OveragePrice <= 0))
        {
            await HttpContext.SendErrorAsync(Error.Validation("plan.sin_overage",
                "Tu plan no admite pedidos extra. Mejora tu plan para habilitarlos."), ct);
            return;
        }

        tenant.AllowOverage = req.Allow;
        tenants.Update(tenant);
        await uow.SaveChangesAsync(ct);

        var usage = await planService.GetUsageAsync(ct);
        await Send.OkAsync(mapper.Map<Domain.Abstractions.PlanUsage, PlanUsageDto>(usage), ct);
    }
}
