using Comanda.Domain.Enums;
using Comanda.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Comanda.Api.Features.Public;

public sealed class RiderCallbackRequest
{
    public Guid JobId { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Open/Accepted/Delivered
    public string RiderName { get; set; } = string.Empty;
    public decimal? ProposedFee { get; set; }
}

/// <summary>
/// Recibe el aviso de RidersHub cuando un rider externo acepta o entrega un pedido.
/// Valida el secreto compartido (X-Callback-Key). Sin JWT (lo llama el microservicio).
/// </summary>
public sealed class RiderCallbackEndpoint(ComandaDbContext db, IConfiguration config)
    : Endpoint<RiderCallbackRequest>
{
    public override void Configure()
    {
        Post("/public/riders/callback");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RiderCallbackRequest req, CancellationToken ct)
    {
        var key = HttpContext.Request.Headers["X-Callback-Key"].FirstOrDefault();
        var expected = config["Riders:CallbackSecret"];
        if (string.IsNullOrWhiteSpace(expected) || key != expected)
        {
            await Send.ResponseAsync(new { ok = false }, 401, ct);
            return;
        }

        if (!Guid.TryParse(req.OrderId, out var orderId)) { await Send.OkAsync(new { ok = true }, ct); return; }

        var order = await db.Orders.IgnoreQueryFilters()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.RiderJobId == req.JobId, ct);
        if (order is null) { await Send.OkAsync(new { ok = true }, ct); return; }

        order.RiderJobStatus = req.Status;
        if (req.Status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
        {
            order.DriverName = req.RiderName;
            order.DispatchedAt = DateTime.UtcNow;
            order.RiderProposedFee = req.ProposedFee;
        }
        else if (req.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
        {
            // El rider ya entregó: avanza el pedido en el kanban aunque la cocina no lo haya movido manualmente.
            order.Status = OrderStatus.Entregados;
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}
