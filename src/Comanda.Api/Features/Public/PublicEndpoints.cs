using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;
using Order = Comanda.Domain.Entities.Order;

namespace Comanda.Api.Features.Public;

// ============================================================
//  Tienda pública (QR) — endpoints anónimos para el cliente final.
//  El comensal escanea el QR de la sucursal, ve el menú y hace pedido
//  sin iniciar sesión. Los precios SIEMPRE los resuelve el servidor.
// ============================================================

public sealed class PublicBranchesRequest { public string Slug { get; set; } = string.Empty; }

/// <summary>Lista las sucursales activas de un restaurante (landing por slug).</summary>
public sealed class PublicBranchesEndpoint(ITenantResolver resolver, IRepository<Branch> branches, IRMapper mapper)
    : Endpoint<PublicBranchesRequest, List<BranchDto>>
{
    public override void Configure()
    {
        Get("/public/t/{slug}/branches");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PublicBranchesRequest req, CancellationToken ct)
    {
        if (!await resolver.ResolveBySlugAsync(req.Slug, ct))
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("tienda.restaurante_no_encontrado", "El restaurante no existe o no está disponible."), ct);
            return;
        }
        var list = await branches.ListAsync(b => b.IsActive, ct);
        await Send.OkAsync(mapper.MapList<Branch, BranchDto>(list), ct);
    }
}

public sealed class PublicMenuRequest { public Guid BranchId { get; set; } }

/// <summary>Devuelve el menú público de una sucursal (categorías + productos disponibles + zonas de entrega).</summary>
public sealed class PublicMenuEndpoint(
    ITenantResolver resolver,
    IRepository<Branch> branches,
    IRepository<Category> categories,
    IProductRepository products,
    IRepository<DeliveryZone> zones,
    IRMapper mapper) : Endpoint<PublicMenuRequest, PublicMenuDto>
{
    public override void Configure()
    {
        Get("/public/branches/{branchId}/menu");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PublicMenuRequest req, CancellationToken ct)
    {
        // Resuelve el tenant a partir de la sucursal (Id global) antes de consultar.
        if (!await resolver.ResolveByBranchAsync(req.BranchId, ct))
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("tienda.sucursal_no_encontrada", "La sucursal no existe o no está disponible."), ct);
            return;
        }
        if (await branches.GetByIdAsync(req.BranchId, ct) is not { IsActive: true } branch)
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("tienda.sucursal_no_encontrada", "La sucursal no existe o no está disponible."), ct);
            return;
        }

        var cats = (await categories.ListAsync(ct)).OrderBy(c => c.SortOrder).ToList();
        var available = (await products.ListWithCategoryAsync(ct))
            .Where(p => p.IsAvailable)
            .OrderBy(p => p.Category != null ? p.Category.SortOrder : 0)
            .ThenBy(p => p.Name)
            .ToList();

        // Zonas activas de la sucursal (o globales, BranchId null).
        var branchZones = (await zones.ListAsync(z => z.IsActive && (z.BranchId == null || z.BranchId == branch.Id), ct))
            .OrderBy(z => z.Fee).ThenBy(z => z.Name).ToList();

        await Send.OkAsync(new PublicMenuDto
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            BranchAddress = branch.Address,
            BranchHours = branch.Hours,
            BranchWhatsapp = branch.WhatsappPhone,
            Categories = mapper.MapList<Category, CategoryDto>(cats),
            Products = mapper.MapList<Product, PublicProductDto>(available),
            DeliveryZones = mapper.MapList<DeliveryZone, DeliveryZoneDto>(branchZones),
            BranchLat = branch.Latitude,
            BranchLng = branch.Longitude,
            CoverageRadiusKm = branch.CoverageRadiusKm,
            DeliveryBaseFee = branch.DeliveryBaseFee,
            DeliveryFeePerKm = branch.DeliveryFeePerKm,
        }, ct);
    }
}

public sealed class PublicOrderLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Variant { get; set; }              // una opción de Product.Variants
    public List<string> Extras { get; set; } = new(); // subconjunto de Product.Extras
}

public sealed class PublicOrderRequest
{
    public Guid BranchId { get; set; }
    public OrderChannel Channel { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty; // requerido en Delivery
    public string Notes { get; set; } = string.Empty;
    public Guid? DeliveryZoneId { get; set; }                   // la tarifa se resuelve en el servidor
    public double? CustomerLat { get; set; }                    // si la sucursal usa radio de cobertura por distancia
    public double? CustomerLng { get; set; }
    public string? CouponCode { get; set; }                     // descuento (validado en el servidor)
    public bool RedeemPoints { get; set; }                      // canjear puntos de fidelización
    public string? LoyaltyToken { get; set; }                   // prueba de que este dispositivo es el dueño del teléfono
    public bool PayOnline { get; set; }                         // iniciar pago en línea (PaymentsHub)
    public string? ReturnUrl { get; set; }                      // a dónde vuelve el cliente tras pagar (lo da el front)
    public decimal TipRestaurant { get; set; }                  // propina opcional para el restaurante
    public decimal TipRider { get; set; }                       // propina opcional para quien entrega (solo Delivery)
    public List<PublicOrderLine> Items { get; set; } = new();
}

public sealed class PublicOrderValidator : Validator<PublicOrderRequest>
{
    public PublicOrderValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Selecciona una sucursal.");
        RuleFor(x => x.CustomerName).NotEmpty().WithMessage("Escribe tu nombre.");
        RuleFor(x => x.CustomerPhone).NotEmpty().WithMessage("Escribe tu teléfono.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Tu pedido está vacío.");
        RuleForEach(x => x.Items).ChildRules(i =>
            i.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero."));
        RuleFor(x => x.CustomerAddress)
            .NotEmpty().When(x => x.Channel == OrderChannel.Delivery)
            .WithMessage("Para delivery necesitamos tu dirección.");
        RuleFor(x => x.ReturnUrl)
            .NotEmpty().When(x => x.PayOnline)
            .WithMessage("Falta la URL de retorno para el pago en línea.");
        RuleFor(x => x.TipRestaurant).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TipRider)
            .Equal(0).When(x => x.Channel != OrderChannel.Delivery)
            .WithMessage("La propina para el rider solo aplica en pedidos a domicilio.");
    }
}

/// <summary>Crea un pedido desde la tienda pública. Cae en el tablero como "Nuevos".</summary>
public sealed class PublicCreateOrderEndpoint(
    ITenantResolver resolver,
    ICurrentTenant currentTenant,
    IRepository<Branch> branches,
    IRepository<Tenant> tenants,
    IProductRepository products,
    IRepository<DeliveryZone> zones,
    IRepository<Customer> customers,
    ICouponService coupons,
    IPaymentsClient payments,
    ISecretProtector secrets,
    IPlanService planService,
    IOrderRepository orders,
    IUnitOfWork uow,
    IRMapper mapper) : Endpoint<PublicOrderRequest, OrderDto>
{
    public override void Configure()
    {
        Post("/public/orders");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PublicOrderRequest req, CancellationToken ct)
    {
        // Resuelve el tenant a partir de la sucursal antes de tocar datos.
        if (!await resolver.ResolveByBranchAsync(req.BranchId, ct))
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("tienda.sucursal_no_encontrada", "La sucursal no existe o no está disponible."), ct);
            return;
        }
        if (await branches.GetByIdAsync(req.BranchId, ct) is not { IsActive: true } branch)
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("tienda.sucursal_no_encontrada", "La sucursal no existe o no está disponible."), ct);
            return;
        }

        var tenant = currentTenant.TenantId is { } tid ? await tenants.GetByIdAsync(tid, ct) : null;

        // Límite de pedidos del plan (con overage si el plan lo permite).
        var orderAllowed = await planService.EnsureOrderAllowedAsync(ct);
        if (orderAllowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(orderAllowed.Error, ct);
            return;
        }

        // Pago en línea: requiere que el plan incluya la función Y que el restaurante tenga
        // su propio Wompi (sin fallback a Innovacors, para no rutear comisiones a otra cuenta).
        if (req.PayOnline)
        {
            var feat = await planService.EnsureFeatureAsync(PlanFeature.OnlinePayments, ct);
            if (feat.IsFailure)
            {
                await HttpContext.SendErrorAsync(feat.Error, ct);
                return;
            }
            if (tenant is not { HasOwnPayments: true })
            {
                await HttpContext.SendErrorAsync(
                    Error.Validation("pago.no_configurado",
                        "Este restaurante aún no tiene configurado el pago en línea. Elige otro método de pago."), ct);
                return;
            }
        }

        // Los precios y nombres SIEMPRE salen del catálogo del servidor (no del cliente).
        var catalog = (await products.ListAsync(ct)).ToDictionary(p => p.Id);
        var lines = new List<OrderItem>();
        foreach (var line in req.Items)
        {
            if (!catalog.TryGetValue(line.ProductId, out var product) || !product.IsAvailable)
            {
                await HttpContext.SendErrorAsync(
                    Error.Validation("tienda.producto_no_disponible", "Uno de los productos ya no está disponible."), ct);
                return;
            }

            // Modificadores: variante y extras deben pertenecer al producto; el PRECIO
            // (base + variante + extras) lo calcula el servidor, no el cliente.
            var unitPrice = product.Price;
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(line.Variant))
            {
                var variant = product.Variants.FirstOrDefault(v => v.Name == line.Variant);
                if (variant is null)
                {
                    await HttpContext.SendErrorAsync(
                        Error.Validation("tienda.variante_invalida", "La opción seleccionada no es válida."), ct);
                    return;
                }
                unitPrice += variant.Price;
                parts.Add(variant.Name);
            }
            foreach (var extraName in line.Extras)
            {
                var extra = product.Extras.FirstOrDefault(x => x.Name == extraName);
                if (extra is null)
                {
                    await HttpContext.SendErrorAsync(
                        Error.Validation("tienda.extra_invalido", "Un complemento seleccionado no es válido."), ct);
                    return;
                }
                unitPrice += extra.Price;
                parts.Add("+" + extra.Name);
            }

            lines.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Modifiers = string.Join(" · ", parts),
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
            });
        }

        // La tarifa se resuelve en el servidor: por radio de cobertura (distancia real) si la sucursal
        // lo tiene configurado, o por zona con nombre (comportamiento anterior) si no.
        var deliveryFee = 0m;
        var zoneName = string.Empty;
        double? distanceKm = null;

        if (req.Channel == OrderChannel.Delivery && branch.CoverageRadiusKm is { } radiusKm)
        {
            if (req.CustomerLat is not { } custLat || req.CustomerLng is not { } custLng)
            {
                await HttpContext.SendErrorAsync(
                    Error.Validation("delivery.ubicacion_requerida", "Selecciona tu ubicación en el mapa para calcular el envío."), ct);
                return;
            }
            if (branch.Latitude is not { } branchLat || branch.Longitude is not { } branchLng)
            {
                await HttpContext.SendErrorAsync(
                    Error.Failure("delivery.sucursal_sin_ubicacion", "La sucursal no tiene ubicación configurada."), ct);
                return;
            }

            var distance = GeoDistance.HaversineKm(branchLat, branchLng, custLat, custLng);
            if (distance > radiusKm)
            {
                await HttpContext.SendErrorAsync(
                    Error.Validation("delivery.fuera_de_cobertura",
                        $"Estás fuera de nuestra zona de cobertura ({distance:F1} km, máximo {radiusKm:F1} km)."), ct);
                return;
            }

            distanceKm = distance;
            deliveryFee = Math.Round(branch.DeliveryBaseFee + branch.DeliveryFeePerKm * (decimal)distance, 2);
            zoneName = $"Cobertura ({distance:F1} km)";
        }
        else if (req.Channel == OrderChannel.Delivery && req.DeliveryZoneId is { } zoneId)
        {
            if (await zones.GetByIdAsync(zoneId, ct) is not { IsActive: true } zone
                || (zone.BranchId != null && zone.BranchId != branch.Id))
            {
                await HttpContext.SendErrorAsync(
                    Error.Validation("delivery.zona_invalida", "La zona de entrega seleccionada no es válida."), ct);
                return;
            }
            deliveryFee = zone.Fee;
            zoneName = zone.Name;
        }

        var itemsSubtotal = lines.Sum(l => l.UnitPrice * l.Quantity);

        // Cliente (CRM): se busca antes para poder canjear puntos.
        var phoneKey = new string(req.CustomerPhone.Where(char.IsDigit).ToArray());
        var customer = phoneKey.Length > 0 ? await customers.FirstOrDefaultAsync(c => c.Phone == phoneKey, ct) : null;

        // Config de fidelización del tenant actual (tenant ya cargado arriba); además gated por plan.
        var loyaltyOn = (tenant?.LoyaltyEnabled ?? false) && await planService.HasFeatureAsync(PlanFeature.Loyalty, ct);

        // Cupón (opcional): el descuento se valida y calcula en el servidor.
        var couponCode = string.Empty;
        var discount = 0m;
        if (!string.IsNullOrWhiteSpace(req.CouponCode))
        {
            var feat = await planService.EnsureFeatureAsync(PlanFeature.Coupons, ct);
            if (feat.IsFailure)
            {
                await HttpContext.SendErrorAsync(feat.Error, ct);
                return;
            }
            var applied = await coupons.ApplyAsync(req.CouponCode, itemsSubtotal, ct);
            if (applied.IsFailure)
            {
                await HttpContext.SendErrorAsync(applied.Error, ct);
                return;
            }
            couponCode = applied.Value.Coupon.Code;
            discount = applied.Value.Discount;
            applied.Value.Coupon.TimesRedeemed++;
        }

        // Canje de puntos (en US$ enteros): no excede los puntos del cliente ni el subtotal restante.
        // Requiere el token del dispositivo que reclamó a este cliente (ver /public/loyalty/lookup):
        // sin esto, cualquiera que solo conozca el teléfono ajeno podría gastar sus puntos.
        var ownsLoyaltyToken = customer is not null && !string.IsNullOrEmpty(customer.LoyaltyToken)
            && customer.LoyaltyToken == req.LoyaltyToken;
        var pointsRedeemed = 0;
        if (req.RedeemPoints && ownsLoyaltyToken && loyaltyOn && customer is { Points: > 0 } && tenant!.LoyaltyRedeemRate > 0)
        {
            var maxByPoints = customer.Points / tenant.LoyaltyRedeemRate;
            var maxByOrder = (int)Math.Floor(itemsSubtotal - discount);
            var redeemAmount = Math.Max(0, Math.Min(maxByPoints, maxByOrder));
            if (redeemAmount > 0)
            {
                discount += redeemAmount;
                pointsRedeemed = redeemAmount * tenant.LoyaltyRedeemRate;
            }
        }

        // Puntos ganados (sobre el subtotal de productos).
        var pointsEarned = loyaltyOn ? (int)Math.Floor(itemsSubtotal * tenant!.LoyaltyEarnRate) : 0;

        var customerName = req.CustomerName.Trim();
        var customerAddress = req.Channel == OrderChannel.Delivery ? req.CustomerAddress.Trim() : string.Empty;

        var count = await orders.CountAsync(ct);
        var order = new Order
        {
            Code = "#" + (1043 + count),
            Channel = req.Channel,
            Table = ChannelLabel(req.Channel),
            Status = OrderStatus.Nuevos,
            CreatedByName = "Tienda online",
            BranchId = branch.Id,
            CustomerName = customerName,
            CustomerPhone = req.CustomerPhone.Trim(),
            CustomerAddress = customerAddress,
            Notes = req.Notes.Trim(),
            DeliveryFee = deliveryFee,
            DeliveryZoneName = zoneName,
            CustomerLat = req.Channel == OrderChannel.Delivery ? req.CustomerLat : null,
            CustomerLng = req.Channel == OrderChannel.Delivery ? req.CustomerLng : null,
            DeliveryDistanceKm = distanceKm,
            CouponCode = couponCode,
            DiscountAmount = discount,
            TipRestaurant = Math.Max(0, req.TipRestaurant),
            TipRider = req.Channel == OrderChannel.Delivery ? Math.Max(0, req.TipRider) : 0,
            PointsEarned = pointsEarned,
            PointsRedeemed = pointsRedeemed,
            Items = lines,
        };
        order.RecalculateTotal();

        // CRM: crea o actualiza el cliente y ajusta su saldo de puntos (gana − canjea).
        if (phoneKey.Length > 0)
        {
            if (customer is null)
            {
                customer = new Customer { Name = customerName, Phone = phoneKey, Address = customerAddress };
                customer.RegisterOrder(order.Total, order.CreatedAt);
                customer.Points += pointsEarned;
                await customers.AddAsync(customer, ct);
            }
            else
            {
                customer.Name = customerName;
                if (!string.IsNullOrWhiteSpace(customerAddress)) customer.Address = customerAddress;
                customer.RegisterOrder(order.Total, order.CreatedAt);
                customer.Points = Math.Max(0, customer.Points - pointsRedeemed + pointsEarned);
                customers.Update(customer);
            }
            order.CustomerId = customer.Id;
        }

        await orders.AddAsync(order, ct);
        await uow.SaveChangesAsync(ct);

        var dto = mapper.Map<Order, OrderDto>(order);

        // Pago en línea (opcional): inicia el cobro en PaymentsHub y devuelve el link.
        if (req.PayOnline && order.Total > 0)
        {
            // El returnUrl lo provee el front (validado arriba cuando PayOnline=true).
            // Llaves Wompi del restaurante (si las tiene); si no, PaymentsHub usa su fallback.
            var wompiAppId = tenant?.WompiAppId;
            var wompiApiSecret = tenant is { } t && !string.IsNullOrEmpty(t.WompiApiSecretEnc)
                ? secrets.Unprotect(t.WompiApiSecretEnc) : null;
            var charge = await payments.CreateOrderChargeAsync(
                order.Total, "USD", $"Pedido {order.Code} · {branch.Name}",
                order.Id.ToString(), branch.TenantId.ToString(), req.ReturnUrl!, wompiAppId, wompiApiSecret, ct);
            if (charge is not null)
            {
                order.PaymentRef = charge.ChargeId.ToString();
                orders.Update(order);
                await uow.SaveChangesAsync(ct);
                dto.PaymentUrl = charge.PayUrl;
            }
        }

        await Send.OkAsync(dto, ct);
    }

    private static string ChannelLabel(OrderChannel channel) => channel switch
    {
        OrderChannel.Llevar => "Para llevar",
        OrderChannel.Delivery => "Delivery",
        _ => "En el local",
    };
}
