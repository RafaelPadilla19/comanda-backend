namespace Comanda.Domain.Abstractions;

/// <summary>Confirma los cambios pendientes de forma transaccional.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
