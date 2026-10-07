using System.Linq.Expressions;
using System.Reflection;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

public class ComandaDbContext(DbContextOptions<ComandaDbContext> options, ICurrentTenant tenant) : DbContext(options)
{
    private readonly ICurrentTenant _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<CountryPaymentMethod> CountryPaymentMethods => Set<CountryPaymentMethod>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<PaymentMethodConfig> PaymentMethods => Set<PaymentMethodConfig>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Tenant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.Property(x => x.Country).HasMaxLength(2).IsRequired();
            e.Property(x => x.LoyaltyEarnRate).HasPrecision(9, 2);
            e.Property(x => x.SubscriptionStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.WompiAppId).HasMaxLength(100);
            e.Ignore(x => x.HasOwnPayments);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Plan>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.PriceMonthly).HasPrecision(18, 2);
            e.Property(x => x.OveragePrice).HasPrecision(18, 2);
        });

        b.Entity<PlatformSetting>(e =>
        {
            e.Property(x => x.WompiAppId).HasMaxLength(100);
            e.Ignore(x => x.HasWompi);
        });

        b.Entity<CountryPaymentMethod>(e =>
        {
            e.Property(x => x.Country).HasMaxLength(2).IsRequired();
            e.Property(x => x.Key).HasMaxLength(60).IsRequired();
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.Emoji).HasMaxLength(16);
            e.HasIndex(x => new { x.Country, x.Key }).IsUnique();
        });

        b.Entity<PlatformAdmin>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        // Enums almacenados como texto (legibilidad y estabilidad ante reordenamientos).
        b.Entity<User>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            e.HasIndex(x => x.Email).IsUnique();   // login es por email: único global
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.Token).HasMaxLength(200).IsRequired();
            e.Property(x => x.ReplacedByToken).HasMaxLength(200);
            e.HasIndex(x => x.Token).IsUnique();   // único: se busca directo por su valor
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Branch>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Hours).HasMaxLength(120);
            e.Property(x => x.WhatsappPhone).HasMaxLength(30);
            e.Property(x => x.DeliveryBaseFee).HasPrecision(18, 2);
            e.Property(x => x.DeliveryFeePerKm).HasPrecision(18, 2);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Emoji).HasMaxLength(16);
            e.Property(x => x.Tint).HasMaxLength(60);
            e.Property(x => x.Description).HasMaxLength(400);
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CategoryId);
            // Variantes y extras como JSON (nombre + costo adicional).
            e.OwnsMany(x => x.Variants, o => o.ToJson());
            e.OwnsMany(x => x.Extras, o => o.ToJson());
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Table).HasMaxLength(60);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.CreatedByName).HasMaxLength(120);
            e.Property(x => x.CustomerName).HasMaxLength(160);
            e.Property(x => x.CustomerPhone).HasMaxLength(40);
            e.Property(x => x.CustomerAddress).HasMaxLength(400);
            e.Property(x => x.Notes).HasMaxLength(400);
            e.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            e.Property(x => x.RiderProposedFee).HasPrecision(18, 2);
            e.Property(x => x.DeliveryZoneName).HasMaxLength(120);
            e.Property(x => x.DriverName).HasMaxLength(160);
            e.Property(x => x.CouponCode).HasMaxLength(40);
            e.Property(x => x.PaymentRef).HasMaxLength(80);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.TipRestaurant).HasPrecision(18, 2);
            e.Property(x => x.TipRider).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(160);
            e.Property(x => x.Modifiers).HasMaxLength(300);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        });

        b.Entity<CashSession>(e =>
        {
            e.Property(x => x.BranchName).HasMaxLength(160);
            e.Property(x => x.CashierName).HasMaxLength(160);
            e.Property(x => x.SalesEfectivo).HasPrecision(18, 2);
            e.Property(x => x.SalesTarjeta).HasPrecision(18, 2);
            e.Property(x => x.SalesTransfer365).HasPrecision(18, 2);
            e.Property(x => x.SalesTransfer).HasPrecision(18, 2);
            e.HasMany(x => x.Movements).WithOne().HasForeignKey(m => m.CashSessionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CashMovement>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(160);
            e.Property(x => x.Sub).HasMaxLength(200);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.Stock).HasPrecision(18, 3);
            e.Property(x => x.Min).HasPrecision(18, 3);
            e.Property(x => x.Cost).HasPrecision(18, 2);
        });

        b.Entity<PaymentMethodConfig>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(60).IsRequired();
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.Emoji).HasMaxLength(16);
            e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });

        b.Entity<Printer>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(60).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.Use).HasMaxLength(160);
            e.Property(x => x.Connection).HasMaxLength(60);
            e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });

        b.Entity<DeliveryZone>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Fee).HasPrecision(18, 2);
            e.HasIndex(x => x.BranchId);
        });

        b.Entity<Driver>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.HasIndex(x => x.BranchId);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40).IsRequired();
            e.Property(x => x.Address).HasMaxLength(400);
            e.Property(x => x.TotalSpent).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.Phone }).IsUnique();
        });

        b.Entity<SubscriptionPayment>(e =>
        {
            e.Property(x => x.PlanName).HasMaxLength(80);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.OverageAmount).HasPrecision(18, 2);
            e.Property(x => x.PaymentRef).HasMaxLength(80);
        });

        b.Entity<Coupon>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Value).HasPrecision(18, 2);
            e.Property(x => x.MinOrder).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        // Las entidades generan su Guid en el constructor (Entity.Id). Decirle a EF
        // que la clave la provee la app evita que un hijo nuevo que entra por una
        // colección/grafo rastreado se interprete como "ya existe" (UPDATE de 0 filas).
        foreach (var et in b.Model.GetEntityTypes().ToList())
        {
            if (et.ClrType.IsSubclassOf(typeof(Entity)))
                b.Entity(et.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
        }

        // Multi-tenant: filtro global por TenantId + índice en cada entidad ITenantScoped.
        foreach (var et in b.Model.GetEntityTypes().ToList())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(et.ClrType))
            {
                b.Entity(et.ClrType).HasIndex(nameof(ITenantScoped.TenantId));
                ApplyTenantFilter
                    .MakeGenericMethod(et.ClrType)
                    .Invoke(this, [b]);
            }
        }
    }

    private static readonly MethodInfo ApplyTenantFilter =
        typeof(ComandaDbContext).GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Aplica el filtro global de tenant a una entidad concreta.</summary>
    private void SetTenantFilter<T>(ModelBuilder b) where T : class, ITenantScoped
        => b.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);

    /// <summary>Estampa el TenantId actual en las entidades nuevas antes de guardar.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_tenant.TenantId is { } tenantId)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                    entry.Entity.TenantId = tenantId;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
