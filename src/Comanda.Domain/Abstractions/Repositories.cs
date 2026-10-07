using Comanda.Domain.Entities;

namespace Comanda.Domain.Abstractions;

/// <summary>Consultas específicas de usuarios que requieren carga de relaciones.</summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListWithBranchAsync(CancellationToken ct = default);
    /// <summary>Busca por Id ignorando el filtro de tenant — necesario en /auth/refresh-token,
    /// que es anónimo (sin JWT) y por lo tanto no tiene tenant ambiente todavía.</summary>
    Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Consultas de pedidos con sus líneas.</summary>
public interface IOrderRepository : IRepository<Order>
{
    Task<IReadOnlyList<Order>> ListWithItemsAsync(CancellationToken ct = default);
    Task<Order?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>Consultas de caja con sus movimientos.</summary>
public interface ICashSessionRepository : IRepository<CashSession>
{
    Task<CashSession?> GetOpenAsync(CancellationToken ct = default);
    Task<CashSession?> GetWithMovementsAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Catálogo con la categoría incluida.</summary>
public interface IProductRepository : IRepository<Product>
{
    Task<IReadOnlyList<Product>> ListWithCategoryAsync(CancellationToken ct = default);
    Task<Product?> GetWithCategoryAsync(Guid id, CancellationToken ct = default);
}
