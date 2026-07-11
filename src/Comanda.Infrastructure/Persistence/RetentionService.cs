using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Comanda.Infrastructure.Persistence;

/// <summary>
/// Guardarraíl de crecimiento de la base: purga el historial de pedidos más antiguo que
/// la retención del plan de cada tenant (Plan.RetentionMonths). Corre al arrancar y luego
/// cada 24 h. Trabaja en su propio scope e ignora el filtro de tenant (no hay tenant fijado
/// en segundo plano), filtrando por TenantId explícito. Los OrderItems caen por cascada (FK).
/// </summary>
public sealed class RetentionService(IServiceScopeFactory scopeFactory, ILogger<RetentionService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeña espera para no competir con el seed/migración del arranque.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await PurgeAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Fallo en la purga de retención de pedidos."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ComandaDbContext>();
        await MaintenanceJobs.PurgeRetentionAsync(db, logger, ct);
    }
}
