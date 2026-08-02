using Comanda.Infrastructure.Persistence;
using FastEndpoints;

namespace Comanda.Api.Features.Jobs;

/// <summary>
/// Endpoints de mantenimiento para entornos serverless (Cloud Run + Cloud Scheduler).
/// Protegidos por el encabezado X-Jobs-Key (config Jobs:Key). Si la key no está configurada,
/// se rechazan siempre (en VPS/local los BackgroundServices hacen este trabajo).
/// </summary>
public abstract class JobEndpointBase : EndpointWithoutRequest
{
    protected bool Authorized(IConfiguration config)
    {
        var expected = config["Jobs:Key"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        var provided = HttpContext.Request.Headers["X-Jobs-Key"].FirstOrDefault();
        return string.Equals(provided, expected, StringComparison.Ordinal);
    }
}

/// <summary>Purga el historial de pedidos según la retención del plan de cada tenant.</summary>
public sealed class RetentionJobEndpoint(ComandaDbContext db, IConfiguration config, ILogger<RetentionJobEndpoint> logger)
    : JobEndpointBase
{
    public override void Configure() { Post("/jobs/retention"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Authorized(config)) { await Send.ResponseAsync(new { ok = false }, 401, ct); return; }
        var purged = await MaintenanceJobs.PurgeRetentionAsync(db, logger, ct);
        await Send.OkAsync(new { ok = true, purged }, ct);
    }
}

/// <summary>Marca PastDue a los tenants de plan pago vencidos más allá de la gracia, y baja a Starter los trials vencidos.</summary>
public sealed class RenewalJobEndpoint(ComandaDbContext db, IConfiguration config, ILogger<RenewalJobEndpoint> logger)
    : JobEndpointBase
{
    public override void Configure() { Post("/jobs/renewal"); AllowAnonymous(); }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Authorized(config)) { await Send.ResponseAsync(new { ok = false }, 401, ct); return; }
        var marked = await MaintenanceJobs.SweepSubscriptionsAsync(db, logger, ct);
        var trialsEnded = await MaintenanceJobs.SweepTrialsAsync(db, logger, ct);
        await Send.OkAsync(new { ok = true, marked, trialsEnded }, ct);
    }
}
