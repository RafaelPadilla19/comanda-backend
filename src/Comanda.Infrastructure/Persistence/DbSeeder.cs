using Comanda.Domain.Abstractions;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

/// <summary>Carga datos iniciales (cocina salvadoreña, USD) si la base está vacía.</summary>
public static class DbSeeder
{
    /// <summary>Contraseña por defecto de los usuarios sembrados (solo para desarrollo).</summary>
    public const string DefaultPassword = "Comanda123*";

    /// <summary>Slug estable del tenant de demostración (datos salvadoreños).</summary>
    public const string DemoSlug = "sabores-del-puerto";

    public static async Task SeedAsync(ComandaDbContext db, IPasswordHasher hasher, ICurrentTenant tenant, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        // ---- Plataforma: planes de suscripción (globales, modelo freemium estilo OlaClick) ----
        await EnsurePlansAsync(db, ct);

        // ---- Plataforma: catálogo de métodos de pago por país (Centroamérica) ----
        await EnsureCountryPaymentMethodsAsync(db, ct);

        // Backfill SOLO del demo salvadoreño creado antes de la columna Country (valor explícito,
        // no un default global): los tenants reales eligen su país en el registro.
        await db.Tenants.IgnoreQueryFilters().Where(t => t.Slug == DemoSlug && t.Country == "")
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Country, "SV"), ct);

        // ---- Plataforma: super-admin del SaaS ----
        if (!await db.PlatformAdmins.AnyAsync(ct))
        {
            db.PlatformAdmins.Add(new PlatformAdmin
            {
                Name = "Super Admin",
                Email = "admin@comanda.sv",
                PasswordHash = hasher.Hash("Admin123*"),
            });
            await db.SaveChangesAsync(ct);
        }

        // Tenant de demostración (estable por slug). El resto de los datos cuelgan de él.
        var demo = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Slug == DemoSlug, ct);
        if (demo is null)
        {
            demo = new Tenant { Name = "Sabores del Puerto", Slug = DemoSlug, Country = "SV" };
            db.Tenants.Add(demo);
            await db.SaveChangesAsync(ct); // Tenant no es ITenantScoped
        }
        // Asigna plan + suscripción activa al demo si aún no tiene (o si apunta a un plan legado).
        var legacyPlanNames = new[] { "Emprendedor", "Negocio", "Cadena" };
        var demoPlan = demo.PlanId is { } dpid ? await db.Plans.FirstOrDefaultAsync(p => p.Id == dpid, ct) : null;
        if (demoPlan is null || legacyPlanNames.Contains(demoPlan.Name))
        {
            var premium = await db.Plans.FirstOrDefaultAsync(p => p.Name == "Premium", ct);
            if (premium is not null)
            {
                demo.PlanId = premium.Id;
                demo.SubscriptionStatus = SubscriptionStatus.Active;
                await db.SaveChangesAsync(ct);
            }
        }
        tenant.Set(demo.Id); // a partir de aquí, el estampado y el filtro apuntan al demo

        // Backfill one-time: los datos pre-multitenant quedaron con TenantId vacío;
        // se los asignamos al tenant demo (idempotente: solo afecta filas en Guid.Empty).
        await BackfillLegacyTenantAsync(db, demo.Id, ct);

        if (await db.Categories.AnyAsync(ct)) return; // ya sembrado

        // ---- Categorías ----
        var entradas = new Category { Name = "Entradas", SortOrder = 0 };
        var fondos = new Category { Name = "Fondos", SortOrder = 1 };
        var bebidas = new Category { Name = "Bebidas", SortOrder = 2 };
        var postres = new Category { Name = "Postres", SortOrder = 3 };
        db.Categories.AddRange(entradas, fondos, bebidas, postres);

        // ---- Productos ----
        // Las opciones llevan costo adicional: (nombre, precio extra sobre el base).
        static List<ProductOption> Opt(params (string Name, decimal Price)[] options) =>
            options.Select(o => new ProductOption { Name = o.Name, Price = o.Price }).ToList();

        Product P(string name, decimal price, string emoji, string tint, Category cat,
                  string desc = "", List<ProductOption>? variants = null, List<ProductOption>? extras = null) => new()
        {
            Name = name, Price = price, Emoji = emoji, Tint = tint, Category = cat, CategoryId = cat.Id,
            Description = desc, Variants = variants ?? [], Extras = extras ?? [],
        };

        db.Products.AddRange(
            P("Pupusas Revueltas", 3.50m, "🫓", "var(--amber-soft)", entradas, "Tres pupusas de queso, frijol y chicharrón con curtido", Opt(("Maíz", 0m), ("Arroz", 0.25m)), Opt(("Curtido extra", 0.25m), ("Queso extra", 0.50m))),
            P("Yuca con Chicharrón", 4.50m, "🍟", "var(--amber-soft)", entradas, "Yuca frita o sancochada con chicharrón y curtido", Opt(("Frita", 0m), ("Sancochada", 0m)), Opt(("Pepescas", 1.00m))),
            P("Enchiladas", 4.00m, "🌮", "var(--red-soft)", entradas, "Tortillas doradas con carne, huevo y salsa", null, Opt(("Aguacate", 0.75m))),
            P("Tamales de Elote", 3.00m, "🌽", "var(--amber-soft)", entradas, "Tamal de elote tierno con crema"),
            P("Mariscada", 12.50m, "🦞", "var(--red-soft)", fondos, "Sopa de mariscos en leche de coco", Opt(("Personal", 0m), ("Familiar", 6.50m)), Opt(("Tortillas", 0.50m), ("Picante", 0m))),
            P("Camarones al Ajillo", 11.00m, "🦐", "var(--primary-soft)", fondos, "Camarones salteados al ajillo con arroz", Opt(("Normal", 0m), ("Doble", 5.00m)), Opt(("Arroz extra", 1.50m))),
            P("Pescado Frito Entero", 10.50m, "🐟", "var(--blue-soft)", fondos, "Pescado entero frito con ensalada y tortillas"),
            P("Pollo Encebollado", 7.50m, "🍗", "var(--amber-soft)", fondos, "Pieza de pollo encebollada con arroz y ensalada", Opt(("Pierna", 0m), ("Pechuga", 1.00m)), Opt(("Tortillas", 0.50m), ("Frijoles", 0.75m))),
            P("Carne Asada", 9.00m, "🥩", "var(--red-soft)", fondos, "Carne asada con chimol, frijoles y queso"),
            P("Sopa de Pata", 6.50m, "🍲", "var(--amber-soft)", fondos, "Sopa tradicional con pata de res y verduras"),
            P("Horchata", 1.75m, "🥛", "var(--violet-soft)", bebidas, "Refresco de morro y semillas", Opt(("Vaso", 0m), ("Jarra", 3.00m))),
            P("Fresco de Ensalada", 1.75m, "🍹", "var(--primary-soft)", bebidas, "Refresco de frutas picadas"),
            P("Kolashampán", 1.25m, "🥤", "var(--amber-soft)", bebidas, "Gaseosa salvadoreña bien fría", Opt(("Lata", 0m), ("Botella", 0.50m))),
            P("Cerveza Pilsener", 2.50m, "🍺", "var(--amber-soft)", bebidas, "Cerveza nacional"),
            P("Café", 1.50m, "☕", "var(--surface-hover)", bebidas, "Café de altura"),
            P("Quesadilla Salvadoreña", 2.25m, "🍰", "var(--amber-soft)", postres, "Pan dulce de queso con ajonjolí"),
            P("Nuégados con Miel", 2.75m, "🍩", "var(--amber-soft)", postres, "Nuégados de yuca bañados en miel de panela"),
            P("Plátano en Miel", 2.50m, "🍌", "var(--amber-soft)", postres, "Plátano maduro en miel con crema"));

        // ---- Sucursales ----
        var centro = new Branch { Name = "Sucursal Centro", Address = "Calle Arce 512, San Salvador", Hours = "Lun–Dom · 11:00 – 22:00", WhatsappPhone = "50370000001" };
        var tecla = new Branch { Name = "Sucursal Santa Tecla", Address = "Paseo El Carmen, Santa Tecla", Hours = "Lun–Dom · 12:00 – 23:00", WhatsappPhone = "50370000002" };
        var libertad = new Branch { Name = "Sucursal La Libertad", Address = "Malecón, Puerto de La Libertad", Hours = "Mar–Dom · 11:00 – 22:00", WhatsappPhone = "50370000003" };
        db.Branches.AddRange(centro, tecla, libertad);

        // ---- Usuarios ----
        var pwd = hasher.Hash(DefaultPassword);
        db.Users.AddRange(
            new User { Name = "Rosa Medina", Email = "rosa@saboresdelpuerto.sv", PasswordHash = pwd, Role = UserRole.Administradora, BranchId = null, IsActive = true },
            new User { Name = "Carlos Ávila", Email = "carlos@saboresdelpuerto.sv", PasswordHash = pwd, Role = UserRole.Cajero, Branch = centro, IsActive = true },
            new User { Name = "Lucía Ponce", Email = "lucia@saboresdelpuerto.sv", PasswordHash = pwd, Role = UserRole.Mesera, Branch = centro, IsActive = true },
            new User { Name = "Diego Salas", Email = "diego@saboresdelpuerto.sv", PasswordHash = pwd, Role = UserRole.Cocina, Branch = tecla, IsActive = true },
            new User { Name = "Ana Ríos", Email = "ana@saboresdelpuerto.sv", PasswordHash = pwd, Role = UserRole.Cajero, Branch = libertad, IsActive = false });

        // ---- Inventario ----
        db.InventoryItems.AddRange(
            new InventoryItem { Name = "Filete de pescado", Category = "Pescados", Stock = 1.2m, Unit = "kg", Min = 5, Cost = 6.5m },
            new InventoryItem { Name = "Camarón", Category = "Mariscos", Stock = 2.0m, Unit = "kg", Min = 4, Cost = 9.0m },
            new InventoryItem { Name = "Masa de maíz", Category = "Abarrotes", Stock = 8, Unit = "kg", Min = 10, Cost = 0.8m },
            new InventoryItem { Name = "Quesillo", Category = "Lácteos", Stock = 3.0m, Unit = "kg", Min = 5, Cost = 4.5m },
            new InventoryItem { Name = "Frijol rojo", Category = "Abarrotes", Stock = 22, Unit = "kg", Min = 12, Cost = 1.2m },
            new InventoryItem { Name = "Chicharrón", Category = "Carnes", Stock = 4.5m, Unit = "kg", Min = 3, Cost = 5.5m },
            new InventoryItem { Name = "Plátano", Category = "Verduras", Stock = 18, Unit = "kg", Min = 10, Cost = 0.5m },
            new InventoryItem { Name = "Arroz", Category = "Abarrotes", Stock = 40, Unit = "kg", Min = 15, Cost = 0.9m },
            new InventoryItem { Name = "Aceite vegetal", Category = "Abarrotes", Stock = 14, Unit = "L", Min = 8, Cost = 2.2m });

        // ---- Métodos de pago ----
        db.PaymentMethods.AddRange(
            new PaymentMethodConfig { Key = "pay-efectivo", Label = "Efectivo", Description = "Pago en caja", Emoji = "💵", IsEnabled = true, SortOrder = 0 },
            new PaymentMethodConfig { Key = "pay-tarjeta", Label = "Tarjeta de crédito / débito", Description = "Visa, Mastercard, Amex", Emoji = "💳", IsEnabled = true, SortOrder = 1 },
            new PaymentMethodConfig { Key = "pay-transfer365", Label = "Transfer365", Description = "Transferencia instantánea entre bancos", Emoji = "📱", IsEnabled = true, SortOrder = 2 },
            new PaymentMethodConfig { Key = "pay-transfer", Label = "Transferencia bancaria", Description = "Banco Agrícola, Cuscatlán, Davivienda", Emoji = "🏦", IsEnabled = true, SortOrder = 3 },
            new PaymentMethodConfig { Key = "pay-online", Label = "Pagos en línea (delivery)", Description = "Checkout web del menú digital", Emoji = "🌐", IsEnabled = false, SortOrder = 4 });

        // ---- Impresoras ----
        db.Printers.AddRange(
            new Printer { Key = "pr-cocina", Name = "Cocina principal", Model = "Térmica 80mm", Use = "Comandas de cocina", Connection = "USB", IsConnected = true },
            new Printer { Key = "pr-caja", Name = "Caja 1", Model = "Térmica 58mm", Use = "Tickets y comprobantes", Connection = "Red (LAN)", IsConnected = true },
            new Printer { Key = "pr-barra", Name = "Barra de bebidas", Model = "Térmica 80mm", Use = "Comandas de barra", Connection = "Bluetooth", IsConnected = false });

        // ---- Caja abierta del día ----
        var session = new CashSession
        {
            BranchId = centro.Id, BranchName = centro.Name, CashierName = "Carlos Ávila",
            IsOpen = true, OpenedAt = DateTime.UtcNow.Date.AddHours(14).AddMinutes(15),
            SalesEfectivo = 420, SalesTarjeta = 360, SalesTransfer365 = 245, SalesTransfer = 145,
            Movements =
            [
                new CashMovement { Label = "Ingreso de efectivo", Sub = "Cambio adicional", Amount = 12, Type = MovementType.Ingreso },
                new CashMovement { Label = "Pago a proveedor", Sub = "Verduras del día", Amount = -22, Type = MovementType.Egreso },
                new CashMovement { Label = "Apertura de caja", Sub = "Fondo inicial", Amount = 50, Type = MovementType.Fondo },
            ],
        };
        db.CashSessions.Add(session);

        // ---- Pedidos ----
        Order O(string code, string table, OrderStatus status, string who, (string name, decimal price, int qty)[] items) =>
            new()
            {
                Code = code, Table = table, Status = status, CreatedByName = who, BranchId = centro.Id,
                Items = items.Select(i => new OrderItem { ProductName = i.name, UnitPrice = i.price, Quantity = i.qty, ProductId = Guid.NewGuid() }).ToList(),
            };

        var orders = new List<Order>
        {
            O("#1042", "Mesa 4", OrderStatus.Nuevos, "Carlos", [("Pupusas Revueltas", 3.50m, 1), ("Horchata", 1.75m, 2)]),
            O("#1041", "Para llevar", OrderStatus.Nuevos, "App", [("Pollo Encebollado", 7.50m, 1), ("Kolashampán", 1.25m, 1)]),
            O("#1039", "Mesa 7", OrderStatus.Preparacion, "Lucía", [("Mariscada", 12.50m, 1), ("Pescado Frito Entero", 10.50m, 1), ("Fresco de Ensalada", 1.75m, 2)]),
            O("#1038", "Mesa 2", OrderStatus.Preparacion, "Carlos", [("Camarones al Ajillo", 11.00m, 2), ("Horchata", 1.75m, 1)]),
            O("#1036", "Mesa 9", OrderStatus.Listos, "Lucía", [("Yuca con Chicharrón", 4.50m, 1), ("Quesadilla Salvadoreña", 2.25m, 1)]),
            O("#1034", "Mesa 1", OrderStatus.Entregados, "Carlos", [("Nuégados con Miel", 2.75m, 1), ("Café", 1.50m, 2)]),
            O("#1033", "Para llevar", OrderStatus.Entregados, "App", [("Carne Asada", 9.00m, 1)]),
        };
        foreach (var o in orders) o.RecalculateTotal();
        db.Orders.AddRange(orders);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Inserta pedidos entregados con fechas pasadas (3, 10 y 20 días atrás) para que
    /// los filtros de rango (Hoy/Semana/Mes) tengan datos distintos. Idempotente: solo
    /// siembra si aún no existen estos pedidos históricos.
    /// </summary>
    public static async Task EnsureHistoricalOrdersAsync(ComandaDbContext db, CancellationToken ct = default)
    {
        // Sucursales para distribuir los pedidos (rendimiento por sucursal real).
        var branches = await db.Branches.OrderBy(b => b.Name).ToListAsync(ct);

        if (!await db.Orders.AnyAsync(o => o.Code.StartsWith("#H"), ct))
        {
            Order H(string code, int daysAgo, Guid? branchId, string table, string who, (string name, decimal price, int qty)[] items)
            {
                var o = new Order
                {
                    Code = code, Table = table, Status = OrderStatus.Entregados, CreatedByName = who, BranchId = branchId,
                    CreatedAt = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-2),
                    Items = items.Select(i => new OrderItem { ProductName = i.name, UnitPrice = i.price, Quantity = i.qty, ProductId = Guid.NewGuid() }).ToList(),
                };
                o.RecalculateTotal();
                return o;
            }

            Guid? B(int i) => branches.Count == 0 ? null : branches[i % branches.Count].Id;

            var hist = new List<Order>
            {
                H("#H01", 3, B(0), "Mesa 3", "Carlos", [("Mariscada", 12.50m, 2), ("Pilsener", 2.50m, 4)]),
                H("#H02", 3, B(1), "Para llevar", "App", [("Pollo Encebollado", 7.50m, 3), ("Kolashampán", 1.25m, 3)]),
                H("#H03", 10, B(2), "Mesa 6", "Lucía", [("Camarones al Ajillo", 11.00m, 2), ("Horchata", 1.75m, 2)]),
                H("#H04", 10, B(1), "Mesa 8", "Carlos", [("Pescado Frito Entero", 10.50m, 3), ("Fresco de Ensalada", 1.75m, 3)]),
                H("#H05", 20, B(2), "Mesa 2", "Lucía", [("Carne Asada", 9.00m, 4), ("Pupusas Revueltas", 3.50m, 6)]),
            };
            db.Orders.AddRange(hist);
            await db.SaveChangesAsync(ct);
        }

        // Backfill: asigna sucursal a cualquier pedido sin ella, repartiéndolos
        // (idempotente: solo afecta pedidos con BranchId null).
        if (branches.Count > 0)
        {
            var orphans = await db.Orders.Where(o => o.BranchId == null).OrderBy(o => o.CreatedAt).ToListAsync(ct);
            for (var i = 0; i < orphans.Count; i++)
                orphans[i].BranchId = branches[i % branches.Count].Id;
            if (orphans.Count > 0) await db.SaveChangesAsync(ct);
        }

        // Backfill: asigna un WhatsApp de demo a las sucursales que aún no lo tengan
        // (para que la tienda pública pueda generar el enlace wa.me). Idempotente.
        var noWhats = await db.Branches.Where(b => b.WhatsappPhone == "" || b.WhatsappPhone == null).OrderBy(b => b.Name).ToListAsync(ct);
        for (var i = 0; i < noWhats.Count; i++)
            noWhats[i].WhatsappPhone = "5037000000" + (i + 1);
        if (noWhats.Count > 0) await db.SaveChangesAsync(ct);

        // Zonas de entrega de demo (globales, aplican a todas las sucursales). Idempotente.
        if (!await db.DeliveryZones.AnyAsync(ct))
        {
            db.DeliveryZones.AddRange(
                new DeliveryZone { Name = "Centro / San Salvador", Fee = 1.50m },
                new DeliveryZone { Name = "Colonia Escalón", Fee = 2.50m },
                new DeliveryZone { Name = "Santa Tecla", Fee = 3.00m },
                new DeliveryZone { Name = "Soyapango / Ilopango", Fee = 3.50m });
            await db.SaveChangesAsync(ct);
        }

        // Repartidores de demo. Idempotente.
        if (!await db.Drivers.AnyAsync(ct))
        {
            db.Drivers.AddRange(
                new Driver { Name = "Mario Hernández", Phone = "7100-1001" },
                new Driver { Name = "José Ramírez", Phone = "7100-1002" });
            await db.SaveChangesAsync(ct);
        }

        // Cupones de demo. Idempotente.
        if (!await db.Coupons.AnyAsync(ct))
        {
            db.Coupons.AddRange(
                new Coupon { Code = "BIENVENIDO", Type = DiscountType.Percentage, Value = 10m, MinOrder = 0m },
                new Coupon { Code = "ENVIO2", Type = DiscountType.Fixed, Value = 2m, MinOrder = 10m });
            await db.SaveChangesAsync(ct);
        }

        // CRM: construye clientes a partir de pedidos online sin cliente asociado. Idempotente.
        var unlinked = await db.Orders
            .Where(o => o.CustomerId == null && o.CreatedByName == "Tienda online" && o.CustomerPhone != "")
            .Include(o => o.Items)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        foreach (var o in unlinked)
        {
            var phoneKey = new string(o.CustomerPhone.Where(char.IsDigit).ToArray());
            if (phoneKey.Length == 0) continue;

            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phoneKey, ct);
            if (customer is null)
            {
                customer = new Customer { Name = o.CustomerName, Phone = phoneKey, Address = o.CustomerAddress };
                db.Customers.Add(customer);
            }
            else if (!string.IsNullOrWhiteSpace(o.CustomerAddress))
            {
                customer.Address = o.CustomerAddress;
            }
            customer.RegisterOrder(o.Total, o.CreatedAt);
            o.CustomerId = customer.Id;
        }
        if (unlinked.Count > 0) await db.SaveChangesAsync(ct);

        // Restaura opciones con precio en productos demo que quedaron sin ellas tras la
        // migración a ProductOption (la columna text[] vieja se eliminó). Idempotente:
        // solo toca productos del demo que aún no tengan variantes/extras.
        var demoOptions = new Dictionary<string, (ProductOption[] variants, ProductOption[] extras)>
        {
            ["Pupusas Revueltas"] = ([new() { Name = "Maíz" }, new() { Name = "Arroz", Price = 0.25m }], [new() { Name = "Curtido extra", Price = 0.25m }, new() { Name = "Queso extra", Price = 0.50m }]),
            ["Mariscada"] = ([new() { Name = "Personal" }, new() { Name = "Familiar", Price = 6.50m }], [new() { Name = "Tortillas", Price = 0.50m }, new() { Name = "Picante" }]),
            ["Camarones al Ajillo"] = ([new() { Name = "Normal" }, new() { Name = "Doble", Price = 5.00m }], [new() { Name = "Arroz extra", Price = 1.50m }]),
            ["Pollo Encebollado"] = ([new() { Name = "Pierna" }, new() { Name = "Pechuga", Price = 1.00m }], [new() { Name = "Tortillas", Price = 0.50m }, new() { Name = "Frijoles", Price = 0.75m }]),
        };
        var demoNames = demoOptions.Keys.ToList();
        var candidates = await db.Products.Where(p => demoNames.Contains(p.Name)).ToListAsync(ct);
        var toFix = candidates.Where(p => p.Variants.Count == 0 && p.Extras.Count == 0).ToList();
        foreach (var p in toFix)
        {
            var (variants, extras) = demoOptions[p.Name];
            p.Variants = variants.ToList();
            p.Extras = extras.ToList();
        }
        if (toFix.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Asegura los 4 planes canónicos (Starter/Inicial/Premium/Elite) del modelo freemium.
    /// Idempotente: agrega solo los que falten por nombre (NO sobrescribe los que el operador
    /// ya editó). Desactiva los planes legados que ya no se ofrecen.
    /// </summary>
    private static async Task EnsurePlansAsync(ComandaDbContext db, CancellationToken ct)
    {
        // (nombre, precio, sucursales, productos, usuarios, pedidos/mes, overage, retenciónMeses,
        //  pagosLinea, cupones, fidelización, reportesAvanzados, inventario, orden)
        var canonical = new (string Name, decimal Price, int Branches, int Products, int Users, int Orders,
            decimal Overage, int Retention, bool Pay, bool Coupons, bool Loyalty, bool Reports, bool Inventory, int Sort)[]
        {
            ("Starter", 0m,    1,  30,   2,   40,   0m,    6,  false, false, false, false, false, 0),
            ("Inicial", 9.90m, 1,  150,  3,   75,   0m,    12, true,  true,  false, false, false, 1),
            ("Premium", 25m,   3,  1000, 10,  400,  0.05m, 24, true,  true,  true,  true,  false, 2),
            ("Elite",   59m,   15, 2000, 50,  3000, 0.05m, 36, true,  true,  true,  true,  true,  3),
        };

        var existing = await db.Plans.ToListAsync(ct);
        foreach (var c in canonical)
        {
            if (existing.Any(p => p.Name == c.Name)) continue;
            db.Plans.Add(new Plan
            {
                Name = c.Name, PriceMonthly = c.Price, MaxBranches = c.Branches, MaxProducts = c.Products,
                MaxUsers = c.Users, MaxOrdersMonth = c.Orders, OveragePrice = c.Overage, RetentionMonths = c.Retention,
                OnlinePayments = c.Pay, Coupons = c.Coupons, Loyalty = c.Loyalty, AdvancedReports = c.Reports,
                Inventory = c.Inventory, SortOrder = c.Sort, IsActive = true,
            });
        }

        // Desactiva los planes legados (siguen existiendo por FKs, pero ya no se ofrecen).
        foreach (var legacy in existing.Where(p => p.Name is "Emprendedor" or "Negocio" or "Cadena" && p.IsActive))
            legacy.IsActive = false;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Siembra el catálogo de métodos de pago por país de Centroamérica (global, editable en
    /// /platform). Idempotente: agrega solo los que falten por (país, clave).
    /// </summary>
    private static async Task EnsureCountryPaymentMethodsAsync(ComandaDbContext db, CancellationToken ct)
    {
        // Métodos básicos manuales para TODOS los países (sin integración).
        (string Key, string Label, string Desc, string Emoji, bool Online, bool Default, int Sort)[] Basics() =>
        [
            ("pay-efectivo", "Efectivo", "Pago en caja / contra entrega", "💵", false, true, 0),
            ("pay-tarjeta", "Tarjeta", "Crédito o débito (Visa, Mastercard)", "💳", false, true, 1),
            ("pay-transfer", "Transferencia bancaria", "Banca en línea", "🏦", false, true, 2),
        ];

        // Solo El Salvador tiene métodos integrados por ahora: Transfer365 + pago en línea (Wompi).
        // Los demás países se agregan desde /platform cuando exista la integración.
        var svExtra = new (string Key, string Label, string Desc, string Emoji, bool Online, bool Default, int Sort)[]
        {
            ("pay-transfer365", "Transfer365", "Transferencia instantánea entre bancos", "📱", false, true, 3),
            ("pay-online", "Pago en línea", "Checkout web del menú digital (Wompi)", "🌐", true, false, 9),
        };

        var countries = new[] { "SV", "GT", "HN", "NI", "CR", "PA" };
        var existing = await db.CountryPaymentMethods.ToListAsync(ct);

        foreach (var country in countries)
        {
            var rows = Basics().ToList();
            if (country == "SV") rows.AddRange(svExtra);

            foreach (var r in rows)
            {
                if (existing.Any(x => x.Country == country && x.Key == r.Key)) continue;
                db.CountryPaymentMethods.Add(new CountryPaymentMethod
                {
                    Country = country, Key = r.Key, Label = r.Label, Description = r.Desc, Emoji = r.Emoji,
                    IsOnline = r.Online, DefaultEnabled = r.Default, SortOrder = r.Sort, IsActive = true,
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Asigna al tenant demo las filas que quedaron sin tenant (migración inicial).</summary>
    private static async Task BackfillLegacyTenantAsync(ComandaDbContext db, Guid tenantId, CancellationToken ct)
    {
        var empty = Guid.Empty;
        await db.Branches.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Users.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Categories.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Products.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Orders.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.OrderItems.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.CashSessions.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.CashMovements.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.InventoryItems.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.PaymentMethods.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Printers.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.DeliveryZones.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Drivers.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
        await db.Customers.IgnoreQueryFilters().Where(x => x.TenantId == empty).ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantId, tenantId), ct);
    }

    /// <summary>
    /// Prepara lo MÍNIMO genérico de un restaurante recién registrado: una sucursal y
    /// métodos de pago comunes. NO siembra menú — cada negocio arma su propio catálogo
    /// (no todos venden lo mismo). Asume que el tenant actual ya está fijado.
    /// </summary>
    public static async Task SeedStarterAsync(ComandaDbContext db, string country, CancellationToken ct = default)
    {
        db.Branches.Add(new Branch
        {
            Name = "Sucursal Principal",
            Address = string.Empty,
            Hours = "Lun–Dom · 11:00 – 22:00",
        });

        // Categorías genéricas para que pueda crear productos de una vez (NO se siembran platos:
        // cada negocio arma su propio menú). El dueño puede renombrarlas o agregar más.
        db.Categories.AddRange(
            new Category { Name = "Entradas", SortOrder = 0 },
            new Category { Name = "Platos fuertes", SortOrder = 1 },
            new Category { Name = "Bebidas", SortOrder = 2 },
            new Category { Name = "Postres", SortOrder = 3 });

        await db.SaveChangesAsync(ct);

        // Métodos de pago según el catálogo del país del restaurante.
        await SyncTenantPaymentMethodsAsync(db, country, ct);
    }

    /// <summary>
    /// Sincroniza los métodos de pago del tenant actual con el catálogo de su país: crea las
    /// filas que falten (con el activado por defecto del catálogo). No borra las existentes,
    /// así se respeta lo que el restaurante ya configuró. Idempotente.
    /// </summary>
    public static async Task SyncTenantPaymentMethodsAsync(ComandaDbContext db, string country, CancellationToken ct = default)
    {
        var catalog = (await db.CountryPaymentMethods
            .Where(c => c.Country == country && c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Label)
            .ToListAsync(ct));

        var existing = (await db.PaymentMethods.ToListAsync(ct)).Select(m => m.Key).ToHashSet();

        foreach (var c in catalog)
        {
            if (existing.Contains(c.Key)) continue;
            db.PaymentMethods.Add(new PaymentMethodConfig
            {
                Key = c.Key, Label = c.Label, Description = c.Description, Emoji = c.Emoji,
                IsEnabled = c.DefaultEnabled, SortOrder = c.SortOrder,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
