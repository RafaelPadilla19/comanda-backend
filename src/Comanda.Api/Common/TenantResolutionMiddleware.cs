using Comanda.Domain.Abstractions;

namespace Comanda.Api.Common;

/// <summary>
/// Fija el tenant actual a partir del claim <c>tenant_id</c> del JWT en peticiones
/// autenticadas. Los endpoints públicos (sin JWT) lo resuelven con <see cref="ITenantResolver"/>.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenant tenant)
    {
        var claim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(claim, out var tenantId))
            tenant.Set(tenantId);

        await next(context);
    }
}
