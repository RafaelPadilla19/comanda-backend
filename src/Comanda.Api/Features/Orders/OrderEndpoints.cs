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

namespace Comanda.Api.Features.Orders;

public sealed class OrderBoardDto
{
    public List<OrderDto> Nuevos { get; set; } = new();
    public List<OrderDto> Preparacion { get; set; } = new();
    public List<OrderDto> Listos { get; set; } = new();
    public List<OrderDto> Entregados { get; set; } = new();
}

public sealed class OrderBoardEndpoint(IOrderRepository orders, IRMapper mapper) : EndpointWithoutRequest<OrderBoardDto>
{
    public override void Configure() => Get("/orders/board");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var all = await orders.ListWithItemsAsync(ct);
        List<OrderDto> Group(OrderStatus s) =>
            mapper.MapList<Order, OrderDto>(all.Where(o => o.Status == s).OrderByDescending(o => o.CreatedAt));

        await Send.OkAsync(new OrderBoardDto
        {
            Nuevos = Group(OrderStatus.Nuevos),
            Preparacion = Group(OrderStatus.Preparacion),
            Listos = Group(OrderStatus.Listos),
            Entregados = Group(OrderStatus.Entregados),
        }, ct);
    }
}

public sealed class ListOrdersEndpoint(IOrderRepository orders, IRMapper mapper) : EndpointWithoutRequest<List<OrderDto>>
{
    public override void Configure() => Get("/orders");

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(mapper.MapList<Order, OrderDto>(await orders.ListWithItemsAsync(ct)), ct);
}

public sealed class GetOrderRequest { public Guid Id { get; set; } }

public sealed class GetOrderEndpoint(IOrderRepository orders, IRMapper mapper) : Endpoint<GetOrderRequest, OrderDto>
{
    public override void Configure() => Get("/orders/{id}");

    public override async Task HandleAsync(GetOrderRequest req, CancellationToken ct)
    {
        if (await orders.GetWithItemsAsync(req.Id, ct) is not { } order)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("pedidos.no_encontrado", "Pedido no encontrado."), ct);
            return;
        }
        await Send.OkAsync(mapper.Map<Order, OrderDto>(order), ct);
    }
}

public sealed class CreateOrderItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Modifiers { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public sealed class CreateOrderRequest
{
    public string Table { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string? CouponCode { get; set; }       // descuento por cupón (validado en servidor)
    public decimal ManualDiscount { get; set; }   // descuento discrecional del cajero (si no hay cupón)
    public List<CreateOrderItem> Items { get; set; } = new();
}

public sealed class CreateOrderValidator : Validator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.Table).NotEmpty().WithMessage("Indica la mesa o el tipo de pedido.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("El pedido debe tener al menos un producto.");
        RuleForEach(x => x.Items).ChildRules(i =>
            i.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero."));
    }
}

public sealed class CreateOrderEndpoint(
    IOrderRepository orders, ICouponService coupons, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<CreateOrderRequest, OrderDto>
{
    public override void Configure() => Post("/orders");

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var orderAllowed = await planService.EnsureOrderAllowedAsync(ct);
        if (orderAllowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(orderAllowed.Error, ct);
            return;
        }

        var items = req.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId, ProductName = i.ProductName, Modifiers = i.Modifiers,
            UnitPrice = i.UnitPrice, Quantity = i.Quantity,
        }).ToList();
        var itemsSubtotal = items.Sum(i => i.UnitPrice * i.Quantity);

        // Cupón (validado en servidor) tiene prioridad; si no, el descuento manual del cajero.
        var couponCode = string.Empty;
        var discount = Math.Min(Math.Max(0m, req.ManualDiscount), itemsSubtotal);
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

        var count = await orders.CountAsync(ct);
        var order = new Order
        {
            Code = "#" + (1043 + count),
            Table = req.Table.Trim(),
            CreatedByName = string.IsNullOrWhiteSpace(req.CreatedByName) ? "POS" : req.CreatedByName.Trim(),
            BranchId = req.BranchId,
            Status = OrderStatus.Nuevos,
            CouponCode = couponCode,
            DiscountAmount = discount,
            Items = items,
        };
        order.RecalculateTotal();

        await orders.AddAsync(order, ct);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<Order, OrderDto>(order), ct);
    }
}

public sealed class MoveOrderRequest
{
    public Guid Id { get; set; }
    public int Direction { get; set; } // +1 avanza, -1 retrocede
}

public sealed class MoveOrderEndpoint(IOrderRepository orders, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<MoveOrderRequest, OrderDto>
{
    public override void Configure() => Post("/orders/{id}/move");

    public override async Task HandleAsync(MoveOrderRequest req, CancellationToken ct)
    {
        if (req.Direction is not (1 or -1))
        {
            await HttpContext.SendErrorAsync(Error.Validation("pedidos.direccion", "La dirección debe ser +1 o -1."), ct);
            return;
        }
        if (await orders.GetWithItemsAsync(req.Id, ct) is not { } order)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("pedidos.no_encontrado", "Pedido no encontrado."), ct);
            return;
        }

        var next = (int)order.Status + req.Direction;
        if (next < (int)OrderStatus.Nuevos || next > (int)OrderStatus.Entregados)
        {
            await HttpContext.SendErrorAsync(
                Error.Validation("pedidos.fuera_de_flujo", "El pedido no puede moverse más en esa dirección."), ct);
            return;
        }

        order.Status = (OrderStatus)next;
        orders.Update(order);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<Order, OrderDto>(order), ct);
    }
}

public sealed class AssignDriverRequest
{
    public Guid Id { get; set; }          // pedido (ruta)
    public Guid DriverId { get; set; }    // repartidor a asignar
}

/// <summary>Asigna un repartidor a un pedido de delivery y lo marca "en camino".</summary>
public sealed class AssignDriverEndpoint(IOrderRepository orders, IRepository<Driver> drivers, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<AssignDriverRequest, OrderDto>
{
    public override void Configure() => Post("/orders/{id}/assign-driver");

    public override async Task HandleAsync(AssignDriverRequest req, CancellationToken ct)
    {
        if (await orders.GetWithItemsAsync(req.Id, ct) is not { } order)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("pedidos.no_encontrado", "Pedido no encontrado."), ct);
            return;
        }
        if (order.Channel != OrderChannel.Delivery)
        {
            await HttpContext.SendErrorAsync(
                Error.Validation("pedidos.no_delivery", "Solo los pedidos a domicilio pueden tener repartidor."), ct);
            return;
        }
        if (await drivers.GetByIdAsync(req.DriverId, ct) is not { } driver)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("delivery.repartidor_no_encontrado", "Repartidor no encontrado."), ct);
            return;
        }

        order.DriverId = driver.Id;
        order.DriverName = driver.Name;
        order.DispatchedAt = DateTime.UtcNow;
        orders.Update(order);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<Order, OrderDto>(order), ct);
    }
}
