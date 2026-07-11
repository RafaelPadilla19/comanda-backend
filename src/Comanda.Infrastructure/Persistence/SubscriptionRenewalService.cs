using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Comanda.Infrastructure.Persistence;

/// <summary>
/// Ciclo de suscripción automático (Opción A, sin tarjeta guardada). Corre al arrancar y
/// cada 12 h: a los tenants de plan PAGO cuya vigencia venció hace más del periodo de gracia
/// los pasa a <see cref="SubscriptionStatus.PastDue"/> (entran pero la app los limita a la
/// pantalla de Suscripción hasta que paguen). Los planes gratis nunca vencen. La suspensión
/// dura (Suspended) sigue siendo solo manual del operador.
/// </summary>
public sealed class SubscriptionRenewalService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionRenewalService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Fallo en el ciclo de suscripción."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ComandaDbContext>();
        await MaintenanceJobs.SweepSubscriptionsAsync(db, logger, ct);
    }
}
