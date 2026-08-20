using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;

namespace Comanda.Api.Features.Loyalty;

// ---------------- Admin: configuración de fidelización ----------------

public sealed class GetLoyaltyConfigEndpoint(IRepository<Tenant> tenants, ICurrentTenant current)
    : EndpointWithoutRequest<LoyaltyConfigDto>
{
    public override void Configure() => Get("/loyalty/config");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenant = current.TenantId is { } id ? await tenants.GetByIdAsync(id, ct) : null;
        await Send.OkAsync(new LoyaltyConfigDto
        {
            Enabled = tenant?.LoyaltyEnabled ?? false,
            EarnRate = tenant?.LoyaltyEarnRate ?? 1m,
            RedeemRate = tenant?.LoyaltyRedeemRate ?? 20,
        }, ct);
    }
}

public sealed class UpdateLoyaltyConfigEndpoint(
    IRepository<Tenant> tenants, ICurrentTenant current, IPlanService planService, IUnitOfWork uow)
    : Endpoint<LoyaltyConfigDto, LoyaltyConfigDto>
{
    public override void Configure() => Put("/loyalty/config");

    public override async Task HandleAsync(LoyaltyConfigDto req, CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("tenant.no_encontrado", "Negocio no encontrado."), ct);
            return;
        }
        // Activar fidelización requiere que el plan la incluya.
        if (req.Enabled)
        {
            var feat = await planService.EnsureFeatureAsync(PlanFeature.Loyalty, ct);
            if (feat.IsFailure)
            {
                await HttpContext.SendErrorAsync(feat.Error, ct);
                return;
            }
        }

        tenant.LoyaltyEnabled = req.Enabled;
        tenant.LoyaltyEarnRate = req.EarnRate < 0 ? 0 : req.EarnRate;
        tenant.LoyaltyRedeemRate = req.RedeemRate < 1 ? 1 : req.RedeemRate;
        tenants.Update(tenant);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(new LoyaltyConfigDto
        {
            Enabled = tenant.LoyaltyEnabled, EarnRate = tenant.LoyaltyEarnRate, RedeemRate = tenant.LoyaltyRedeemRate,
        }, ct);
    }
}

// ---------------- Público: saldo de puntos del cliente ----------------

public sealed class LoyaltyLookupRequest
{
    public Guid BranchId { get; set; }
    public string Phone { get; set; } = string.Empty;
    /// <summary>Token guardado en este dispositivo de una consulta anterior. Si viene y coincide,
    /// se usa en vez del teléfono (así el dispositivo dueño no tiene que volver a escribirlo).</summary>
    public string Token { get; set; } = string.Empty;
}

public sealed class PublicLoyaltyLookupEndpoint(
    ITenantResolver resolver, IRepository<Tenant> tenants, IRepository<Customer> customers,
    ICurrentTenant current, IUnitOfWork uow)
    : Endpoint<LoyaltyLookupRequest, LoyaltyLookupDto>
{
    public override void Configure()
    {
        Post("/public/loyalty/lookup");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("loyalty-lookup"));
    }

    public override async Task HandleAsync(LoyaltyLookupRequest req, CancellationToken ct)
    {
        if (!await resolver.ResolveByBranchAsync(req.BranchId, ct) || current.TenantId is not { } tenantId)
        {
            await Send.OkAsync(new LoyaltyLookupDto { Enabled = false }, ct);
            return;
        }

        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null || !tenant.LoyaltyEnabled)
        {
            await Send.OkAsync(new LoyaltyLookupDto { Enabled = false }, ct);
            return;
        }

        // Si el dispositivo ya trae un token válido, ese manda (no expone nada nuevo).
        // Si no, se busca por teléfono como antes (primer contacto / dispositivo nuevo).
        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(req.Token))
            customer = await customers.FirstOrDefaultAsync(c => c.LoyaltyToken == req.Token, ct);

        var tokenAlreadyOwned = customer is not null;
        if (customer is null)
        {
            var phoneKey = new string(req.Phone.Where(char.IsDigit).ToArray());
            customer = phoneKey.Length > 0 ? await customers.FirstOrDefaultAsync(c => c.Phone == phoneKey, ct) : null;
        }

        var points = customer?.Points ?? 0;

        // Solo se emite el token la primera vez que alguien reclama a este cliente (nadie más lo
        // había consultado antes). Si ya tenía token y no vino por ese mismo token, no se revela:
        // así un desconocido que solo sabe el teléfono no puede robar el token para canjear puntos.
        var tokenToReturn = string.Empty;
        if (customer is not null)
        {
            if (tokenAlreadyOwned)
            {
                tokenToReturn = req.Token;
            }
            else if (string.IsNullOrEmpty(customer.LoyaltyToken))
            {
                customer.LoyaltyToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                customers.Update(customer);
                await uow.SaveChangesAsync(ct);
                tokenToReturn = customer.LoyaltyToken;
            }
        }

        await Send.OkAsync(new LoyaltyLookupDto
        {
            Enabled = true,
            Points = points,
            RedeemRate = tenant.LoyaltyRedeemRate,
            RedeemableAmount = tenant.LoyaltyRedeemRate > 0 ? points / tenant.LoyaltyRedeemRate : 0,
            CustomerName = customer?.Name ?? string.Empty,
            Token = tokenToReturn,
        }, ct);
    }
}
