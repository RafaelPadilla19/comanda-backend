using Comanda.Domain.Abstractions;
using Comanda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

public sealed class UserRepository(ComandaDbContext db) : Repository<User>(db), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        // El login ocurre antes de conocer el tenant: se ignora el filtro global.
        => Db.Users.IgnoreQueryFilters().Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdIgnoringTenantAsync(Guid id, CancellationToken ct = default)
        // Igual que GetByEmailAsync: /auth/refresh-token es anónimo (sin JWT), no hay tenant
        // ambiente todavía — el filtro global dejaría esto en null siempre.
        => Db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<User>> ListWithBranchAsync(CancellationToken ct = default)
        => await Db.Users.AsNoTracking().Include(u => u.Branch).OrderBy(u => u.Name).ToListAsync(ct);
}

public sealed class ProductRepository(ComandaDbContext db) : Repository<Product>(db), IProductRepository
{
    public async Task<IReadOnlyList<Product>> ListWithCategoryAsync(CancellationToken ct = default)
        => await Db.Products.AsNoTracking().Include(p => p.Category).OrderBy(p => p.Name).ToListAsync(ct);

    public Task<Product?> GetWithCategoryAsync(Guid id, CancellationToken ct = default)
        => Db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);
}

public sealed class OrderRepository(ComandaDbContext db) : Repository<Order>(db), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> ListWithItemsAsync(CancellationToken ct = default)
        => await Db.Orders.AsNoTracking().Include(o => o.Items).OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public Task<Order?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => Db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await Db.Orders.AsNoTracking().Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => Db.Orders.CountAsync(ct);
}

public sealed class CashSessionRepository(ComandaDbContext db) : Repository<CashSession>(db), ICashSessionRepository
{
    public Task<CashSession?> GetOpenAsync(CancellationToken ct = default)
        => Db.CashSessions.Include(s => s.Movements)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(s => s.IsOpen, ct);

    public Task<CashSession?> GetWithMovementsAsync(Guid id, CancellationToken ct = default)
        => Db.CashSessions.Include(s => s.Movements).FirstOrDefaultAsync(s => s.Id == id, ct);
}
