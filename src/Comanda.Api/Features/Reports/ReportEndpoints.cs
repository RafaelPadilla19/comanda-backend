using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using FastEndpoints;
using RMapper.Core.Interfaces;
using Order = Comanda.Domain.Entities.Order;

namespace Comanda.Api.Features.Reports;

// ----- DTOs de reporte -----

public sealed class TopProductDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Ingresos { get; set; }
}

public sealed class LowStockDto
{
    public string Nombre { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public sealed class CategorySplitDto
{
    public string Categoria { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public int Porcentaje { get; set; }
}

public sealed class DashboardDto
{
    public decimal VentasHoy { get; set; }
    public int PedidosActivos { get; set; }
    public decimal TicketPromedio { get; set; }
    public List<TopProductDto> TopProductos { get; set; } = new();
    public List<LowStockDto> InventarioBajo { get; set; } = new();
    public List<OrderDto> PedidosActivosLista { get; set; } = new();
}

public sealed class SalesReportDto
{
    public decimal TotalVentas { get; set; }
    public List<TopProductDto> TopProductos { get; set; } = new();
    public List<CategorySplitDto> VentasPorCategoria { get; set; } = new();
}

public sealed class RevenuePointDto
{
    public string Date { get; set; } = string.Empty;   // yyyy-MM-dd
    public string Label { get; set; } = string.Empty;  // dd
    public decimal Monto { get; set; }
}

public sealed class BranchPerfDto
{
    public string Nombre { get; set; } = string.Empty;
    public decimal Ventas { get; set; }
    public int Pedidos { get; set; }
    public decimal Ticket { get; set; }
}

// ----- Helpers compartidos -----

/// <summary>Rango temporal para filtrar reportes (hoy / semana / mes).</summary>
public sealed class RangeRequest
{
    public string? Range { get; set; }
}

internal static class ReportHelpers
{
    public static string StockStatus(InventoryItem i) =>
        i.Stock < i.Min ? "Crítico" : i.Stock < i.Min * 1.75m ? "Bajo" : "En stock";

    /// <summary>Inicio (UTC) de la ventana según el rango. 'today' por defecto.</summary>
    public static DateTime RangeStart(string? range) => range?.ToLowerInvariant() switch
    {
        "week" => DateTime.UtcNow.Date.AddDays(-6),
        "month" => DateTime.UtcNow.Date.AddDays(-29),
        _ => DateTime.UtcNow.Date,
    };

    public static List<TopProductDto> TopProducts(IEnumerable<Order> orders, int take) =>
        orders.SelectMany(o => o.Items)
            .GroupBy(it => it.ProductName)
            .Select(g => new TopProductDto
            {
                Nombre = g.Key,
                Cantidad = g.Sum(x => x.Quantity),
                Ingresos = g.Sum(x => x.UnitPrice * x.Quantity),
            })
            .OrderByDescending(p => p.Cantidad)
            .Take(take)
            .ToList();
}

// ----- Endpoints -----

public sealed class DashboardEndpoint(
    IOrderRepository orders, IRepository<InventoryItem> inventory, IRMapper mapper)
    : Endpoint<RangeRequest, DashboardDto>
{
    public override void Configure() => Get("/reports/dashboard");

    public override async Task HandleAsync(RangeRequest req, CancellationToken ct)
    {
        var allOrders = await orders.ListWithItemsAsync(ct);
        var items = await inventory.ListAsync(ct);

        var start = ReportHelpers.RangeStart(req.Range);
        var inRange = allOrders.Where(o => o.CreatedAt >= start).ToList();

        // Ventas del rango = suma de los totales de los pedidos creados en la ventana.
        var ventas = inRange.Sum(o => o.Total);
        var activos = allOrders.Where(o => o.Status != OrderStatus.Entregados).ToList();
        var ticket = inRange.Count == 0 ? 0m : Math.Round(ventas / inRange.Count, 2);

        await Send.OkAsync(new DashboardDto
        {
            VentasHoy = ventas,
            PedidosActivos = activos.Count,
            TicketPromedio = ticket,
            TopProductos = ReportHelpers.TopProducts(inRange, 5),
            InventarioBajo = items
                .Where(i => ReportHelpers.StockStatus(i) != "En stock")
                .OrderBy(i => i.Stock / Math.Max(i.Min, 0.001m))
                .Select(i => new LowStockDto { Nombre = i.Name, Stock = i.Stock, Unit = i.Unit, Estado = ReportHelpers.StockStatus(i) })
                .ToList(),
            PedidosActivosLista = mapper.MapList<Order, OrderDto>(activos.OrderByDescending(o => o.CreatedAt)),
        }, ct);
    }
}

public sealed class SalesReportEndpoint(IOrderRepository orders, IProductRepository products)
    : Endpoint<RangeRequest, SalesReportDto>
{
    public override void Configure() => Get("/reports/sales");

    public override async Task HandleAsync(RangeRequest req, CancellationToken ct)
    {
        var start = ReportHelpers.RangeStart(req.Range);
        var inRange = (await orders.ListWithItemsAsync(ct)).Where(o => o.CreatedAt >= start).ToList();
        var catalog = await products.ListWithCategoryAsync(ct);
        var categoryByName = catalog
            .GroupBy(p => p.Name)
            .ToDictionary(g => g.Key, g => g.First().Category?.Name ?? "Otros", StringComparer.OrdinalIgnoreCase);

        var byCategory = inRange.SelectMany(o => o.Items)
            .GroupBy(it => categoryByName.TryGetValue(it.ProductName, out var c) ? c : "Otros")
            .Select(g => new { Categoria = g.Key, Monto = g.Sum(x => x.UnitPrice * x.Quantity) })
            .OrderByDescending(x => x.Monto)
            .ToList();
        var totalCat = byCategory.Sum(x => x.Monto);

        await Send.OkAsync(new SalesReportDto
        {
            TotalVentas = inRange.Sum(o => o.Total),
            TopProductos = ReportHelpers.TopProducts(inRange, 5),
            VentasPorCategoria = byCategory.Select(x => new CategorySplitDto
            {
                Categoria = x.Categoria,
                Monto = x.Monto,
                Porcentaje = totalCat == 0 ? 0 : (int)Math.Round(x.Monto / totalCat * 100),
            }).ToList(),
        }, ct);
    }
}

public sealed class RevenueSeriesRequest
{
    public int Days { get; set; } = 7;
}

/// <summary>Serie de ingresos por día (últimos N días) a partir de los pedidos.</summary>
public sealed class RevenueSeriesEndpoint(IOrderRepository orders)
    : Endpoint<RevenueSeriesRequest, List<RevenuePointDto>>
{
    public override void Configure() => Get("/reports/revenue-series");

    public override async Task HandleAsync(RevenueSeriesRequest req, CancellationToken ct)
    {
        var days = Math.Clamp(req.Days <= 0 ? 7 : req.Days, 1, 60);
        var all = await orders.ListWithItemsAsync(ct);
        var today = DateTime.UtcNow.Date;

        var points = Enumerable.Range(0, days).Select(offset =>
        {
            var day = today.AddDays(-(days - 1 - offset));
            return new RevenuePointDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Label = day.ToString("dd"),
                Monto = all.Where(o => o.CreatedAt.Date == day).Sum(o => o.Total),
            };
        }).ToList();

        await Send.OkAsync(points, ct);
    }
}

/// <summary>Rendimiento por sucursal: ventas, pedidos y ticket promedio reales.</summary>
public sealed class BranchPerfEndpoint(IOrderRepository orders, IRepository<Branch> branches)
    : EndpointWithoutRequest<List<BranchPerfDto>>
{
    public override void Configure() => Get("/reports/branches");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var all = await orders.ListWithItemsAsync(ct);
        var branchList = await branches.ListAsync(ct);

        var result = branchList
            .Select(b =>
            {
                var ordersOfBranch = all.Where(o => o.BranchId == b.Id).ToList();
                var ventas = ordersOfBranch.Sum(o => o.Total);
                return new BranchPerfDto
                {
                    Nombre = b.Name,
                    Ventas = ventas,
                    Pedidos = ordersOfBranch.Count,
                    Ticket = ordersOfBranch.Count == 0 ? 0 : Math.Round(ventas / ordersOfBranch.Count, 2),
                };
            })
            .OrderByDescending(x => x.Ventas)
            .ToList();

        await Send.OkAsync(result, ct);
    }
}
