using System.Linq.Expressions;
using Comanda.Domain.Common;

namespace Comanda.Domain.Abstractions;

/// <summary>
/// Repositorio genérico, agnóstico del motor de base de datos / ORM.
/// Los predicados son expresiones LINQ estándar (no atan a EF ni a Dapper).
/// </summary>
public interface IRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
