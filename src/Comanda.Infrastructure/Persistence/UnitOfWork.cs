using Comanda.Domain.Abstractions;

namespace Comanda.Infrastructure.Persistence;

public sealed class UnitOfWork(ComandaDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
