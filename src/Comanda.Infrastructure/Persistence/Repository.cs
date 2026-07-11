using System.Linq.Expressions;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

/// <summary>Implementación EF Core del repositorio genérico. Aislada tras <see cref="IRepository{T}"/>.</summary>
public class Repository<TEntity>(ComandaDbContext db) : IRepository<TEntity> where TEntity : Entity
{
    protected readonly ComandaDbContext Db = db;
    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await Set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(predicate, ct);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => Set.AnyAsync(predicate, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);
}
