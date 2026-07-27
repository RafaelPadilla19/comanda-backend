using Comanda.Domain.Abstractions;
using Comanda.Infrastructure.Persistence;
using Comanda.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comanda.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Comanda")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'Comanda'. Configúrala en appsettings o en la variable de entorno ConnectionStrings__Comanda.");

        services.AddDbContext<ComandaDbContext>(opt => opt.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        // Multi-tenant: contexto del tenant (Scoped) y resolutor para endpoints públicos
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IPlanService, PlanService>();

        // Trabajos de mantenimiento. En proceso (VPS/local, default) corren como BackgroundService;
        // en Cloud Run (escala a cero) se apagan con Jobs__RunInProcess=false y los dispara
        // Cloud Scheduler vía los endpoints /jobs/* (misma lógica: MaintenanceJobs).
        if (config.GetValue("Jobs:RunInProcess", true))
        {
            // Guardarraíl de crecimiento de BD: purga el historial según la retención del plan.
            services.AddHostedService<RetentionService>();
            // Ciclo de suscripción: marca vencidos (PastDue) tras el periodo de gracia.
            services.AddHostedService<SubscriptionRenewalService>();
        }

        // Cliente del microservicio de pagos (PaymentsHub) — HttpClient tipado.
        var paymentsUrl = config["Services:PaymentsHubUrl"] ?? "http://localhost:5060";
        services.AddHttpClient<IPaymentsClient, Payments.PaymentsHubClient>(c => c.BaseAddress = new Uri(paymentsUrl));

        // Cliente del microservicio de riders independientes (RidersHub) — HttpClient tipado.
        var ridersUrl = config["Services:RidersHubUrl"] ?? "http://localhost:5062";
        services.AddHttpClient<IRidersClient, Riders.RidersHubClient>(c => c.BaseAddress = new Uri(ridersUrl));

        // Repositorios (genérico + específicos) y unidad de trabajo
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Seguridad
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
