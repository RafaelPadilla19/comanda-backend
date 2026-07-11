using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;

namespace Comanda.Api.Features.Settings;

/// <summary>Devuelve si el restaurante tiene Wompi conectado (sin exponer el secret).</summary>
public sealed class GetWompiConfigEndpoint(IRepository<Tenant> tenants, ICurrentTenant current)
    : EndpointWithoutRequest<WompiConfigDto>
{
    public override void Configure() => Get("/settings/payments/wompi");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenant = current.TenantId is { } id ? await tenants.GetByIdAsync(id, ct) : null;
        await Send.OkAsync(new WompiConfigDto
        {
            AppId = tenant?.WompiAppId ?? string.Empty,
            Connected = tenant?.HasOwnPayments ?? false,
        }, ct);
    }
}

public sealed class SaveWompiConfigRequest
{
    public string AppId { get; set; } = string.Empty;
    /// <summary>Vacío = conservar el secret actual; con valor = reemplazarlo. Para desconectar, vaciar AppId.</summary>
    public string ApiSecret { get; set; } = string.Empty;
}

/// <summary>Guarda las llaves Wompi del restaurante (el secret se cifra en reposo).</summary>
public sealed class SaveWompiConfigEndpoint(
    IRepository<Tenant> tenants, ICurrentTenant current, ISecretProtector secrets, IUnitOfWork uow)
    : Endpoint<SaveWompiConfigRequest, WompiConfigDto>
{
    public override void Configure() => Put("/settings/payments/wompi");

    public override async Task HandleAsync(SaveWompiConfigRequest req, CancellationToken ct)
    {
        if (current.TenantId is not { } id || await tenants.GetByIdAsync(id, ct) is not { } tenant)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("tenant.no_encontrado", "Negocio no encontrado."), ct);
            return;
        }

        var appId = req.AppId.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            // Desconectar pagos propios.
            tenant.WompiAppId = string.Empty;
            tenant.WompiApiSecretEnc = string.Empty;
        }
        else
        {
            tenant.WompiAppId = appId;
            if (!string.IsNullOrWhiteSpace(req.ApiSecret))
                tenant.WompiApiSecretEnc = secrets.Protect(req.ApiSecret.Trim());
        }

        tenants.Update(tenant);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(new WompiConfigDto { AppId = tenant.WompiAppId, Connected = tenant.HasOwnPayments }, ct);
    }
}
