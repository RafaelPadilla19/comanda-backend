using Comanda.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comanda.Api.Design;

/// <summary>
/// Fábrica usada solo por las herramientas de EF (dotnet ef) para crear migraciones
/// sin arrancar el host completo. No requiere conexión activa para generar migraciones.
/// </summary>
public sealed class ComandaDbContextFactory : IDesignTimeDbContextFactory<ComandaDbContext>
{
    public ComandaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Comanda")
            ?? "Host=localhost;Port=5432;Database=comanda;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ComandaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ComandaDbContext(options, new CurrentTenant());
    }
}
