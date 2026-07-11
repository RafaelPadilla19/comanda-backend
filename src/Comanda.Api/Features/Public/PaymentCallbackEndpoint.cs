using Comanda.Domain.Enums;
using Comanda.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Comanda.Api.Features.Public;

public sealed class PaymentCallbackRequest
{
    public Guid ChargeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Kind { get; set; } = "Order";
    public string? OrderRef { get; set; }
}

/// <summary>
/// Recibe el aviso de PaymentsHub cuando un cobro cambia de estado. Valida el secreto
/// compartido (X-Callback-Key). Sin JWT (lo llama el microservicio) → resuelve ignorando
/// el filtro de tenant. Maneja dos tipos: pedido (marca pagado) y suscripción (activa el plan).
/// </summary>
public sealed class PaymentCallbackEndpoint(ComandaDbContext db, IConfiguration config)
    : Endpoint<PaymentCallbackRequest>
{
    public override void Configure()
    {
        Post("/public/payments/callback");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PaymentCallbackRequest req, CancellationToken ct)
    {
        var key = HttpContext.Request.Headers["X-Callback-Key"].FirstOrDefault();
        var expected = config["Payments:CallbackSecret"];
        if (string.IsNullOrWhiteSpace(expected) || key != expected)
        {
            await Send.ResponseAsync(new { ok = false }, 401, ct);
            return;
        }

        var paid = req.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase);

        if (req.Kind.Equals("Subscription", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSubscriptionAsync(req, paid, ct);
        }
        else
        {
            var order = Guid.TryParse(req.OrderRef, out var oid)
                ? await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == oid, ct)
                : await db.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.PaymentRef == req.ChargeId.ToString(), ct);
            if (order is not null && paid && !order.IsPaid)
            {
                order.IsPaid = true;
                await db.SaveChangesAsync(ct);
            }
        }

        await Send.OkAsync(new { ok = true }, ct);
    }

    private async Task HandleSubscriptionAsync(PaymentCallbackRequest req, bool paid, CancellationToken ct)
    {
        if (!paid || !Guid.TryParse(req.OrderRef, out var subId)) return;

        var sub = await db.SubscriptionPayments.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == subId, ct);
        if (sub is null || sub.IsPaid) return;

        sub.IsPaid = true;
        sub.PaidAt = DateTime.UtcNow;

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == sub.TenantId, ct);
        if (tenant is not null)
        {
            // Aplica el plan comprado (mejora/cambio) y extiende la vigencia desde el mayor
            // entre hoy y la vigencia actual (renovación acumulativa).
            tenant.PlanId = sub.PlanId;
            var baseDate = tenant.SubscriptionEndsAt is { } end && end > DateTime.UtcNow ? end : DateTime.UtcNow;
            tenant.SubscriptionEndsAt = baseDate.AddMonths(sub.PeriodMonths);
            tenant.SubscriptionStatus = SubscriptionStatus.Active;

            // Marca como facturado el overage cobrado en este pago (evita volverlo a cobrar).
            if (sub.OverageOrders > 0)
            {
                if (tenant.OverageCycleKey == sub.OverageCycleKey)
                    tenant.OverageBilledOrders += sub.OverageOrders;
                else
                {
                    tenant.OverageCycleKey = sub.OverageCycleKey;
                    tenant.OverageBilledOrders = sub.OverageOrders;
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
