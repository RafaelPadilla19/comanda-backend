using Comanda.Api.Features.Reports;
using Comanda.Domain.Abstractions;
using FastEndpoints;
using Order = Comanda.Domain.Entities.Order;

namespace Comanda.Api.Features.Insights;

// ----- DTOs -----

/// <summary>Tarjeta de hallazgo calculada a partir de los datos del negocio (sin IA externa).</summary>
public sealed class InsightCardDto
{
    public string Icon { get; set; } = "✨";
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral"; // good | warn | neutral
}

/// <summary>Par de productos que se piden juntos con frecuencia (market basket).</summary>
public sealed class ComboPairDto
{
    public string ProductA { get; set; } = string.Empty;
    public string ProductB { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Suggestion { get; set; } = string.Empty;
}

public sealed class InsightsDto
{
    public bool HasData { get; set; }
    public int OrdersAnalyzed { get; set; }
    public List<InsightCardDto> Cards { get; set; } = new();
    public List<ComboPairDto> Combos { get; set; } = new();
}

/// <summary>
/// "Inteligencia del negocio": hallazgos y recomendaciones derivados de los propios pedidos
/// del restaurante mediante estadística/heurística (cero costo, sin IA externa).
/// </summary>
public sealed class InsightsEndpoint(IOrderRepository orders) : Endpoint<RangeRequest, InsightsDto>
{
    // El Salvador es UTC-6 todo el año; convertimos para que hora pico/mejor día sean locales.
    private const int SvOffsetHours = -6;

    private static readonly string[] DiasEs =
        { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };

    public override void Configure() => Get("/reports/insights");

    public override async Task HandleAsync(RangeRequest req, CancellationToken ct)
    {
        // Por defecto analizamos el último mes (más datos = mejores hallazgos).
        var start = ReportHelpers.RangeStart(req.Range ?? "month");
        var all = await orders.ListWithItemsAsync(ct);
        var inRange = all.Where(o => o.CreatedAt >= start).ToList();

        var dto = new InsightsDto { OrdersAnalyzed = inRange.Count, HasData = inRange.Count > 0 };
        if (!dto.HasData)
        {
            await Send.OkAsync(dto, ct);
            return;
        }

        dto.Cards = BuildCards(inRange, all);
        dto.Combos = BuildCombos(inRange);
        await Send.OkAsync(dto, ct);
    }

    private static List<InsightCardDto> BuildCards(List<Order> inRange, IReadOnlyList<Order> all)
    {
        var cards = new List<InsightCardDto>();
        var lines = inRange.SelectMany(o => o.Items).ToList();

        // 1) Producto estrella (por unidades vendidas).
        var totalUnits = lines.Sum(l => l.Quantity);
        var star = lines.GroupBy(l => l.ProductName)
            .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty).FirstOrDefault();
        if (star is not null && totalUnits > 0)
        {
            var share = (int)Math.Round(100.0 * star.Qty / totalUnits);
            cards.Add(new InsightCardDto
            {
                Icon = "⭐", Title = "Tu producto estrella", Value = star.Name, Tone = "good",
                Detail = $"{star.Qty} vendidos · {share}% de tus ventas",
            });
        }

        // 2) Hora pico (por cantidad de pedidos, en hora local SV).
        var byHour = inRange.GroupBy(o => o.CreatedAt.AddHours(SvOffsetHours).Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).FirstOrDefault();
        if (byHour is not null)
            cards.Add(new InsightCardDto
            {
                Icon = "⏰", Title = "Tu hora pico", Value = HourLabel(byHour.Hour),
                Detail = $"{byHour.Count} pedido(s) en esa franja",
            });

        // 3) Mejor día de la semana (por ventas).
        var byDay = inRange.GroupBy(o => (int)o.CreatedAt.AddHours(SvOffsetHours).DayOfWeek)
            .Select(g => new { Day = g.Key, Sales = g.Sum(o => o.Total) })
            .OrderByDescending(x => x.Sales).FirstOrDefault();
        if (byDay is not null)
            cards.Add(new InsightCardDto
            {
                Icon = "📅", Title = "Tu mejor día", Value = DiasEs[byDay.Day],
                Detail = $"{Money(byDay.Sales)} en ventas",
            });

        // 4) Ticket promedio.
        var ventas = inRange.Sum(o => o.Total);
        var ticket = inRange.Count == 0 ? 0m : ventas / inRange.Count;
        cards.Add(new InsightCardDto
        {
            Icon = "🧾", Title = "Ticket promedio", Value = Money(ticket),
            Detail = $"{inRange.Count} pedido(s) analizados",
        });

        // 5) Tendencia: últimos 7 días vs los 7 anteriores.
        var now = DateTime.UtcNow;
        var last7 = all.Where(o => o.CreatedAt >= now.AddDays(-7)).Sum(o => o.Total);
        var prev7 = all.Where(o => o.CreatedAt >= now.AddDays(-14) && o.CreatedAt < now.AddDays(-7)).Sum(o => o.Total);
        if (prev7 > 0)
        {
            var delta = (int)Math.Round(100m * (last7 - prev7) / prev7);
            cards.Add(new InsightCardDto
            {
                Icon = delta >= 0 ? "📈" : "📉",
                Title = "Tendencia semanal",
                Value = (delta >= 0 ? "+" : "") + delta + "%",
                Detail = delta >= 0 ? "Vendiste más que la semana pasada" : "Vendiste menos que la semana pasada",
                Tone = delta >= 0 ? "good" : "warn",
            });
        }

        // 6) Canal más fuerte (Mesa / Delivery / Para llevar, por el campo Table).
        var byChannel = inRange.GroupBy(o => Channel(o.Table))
            .Select(g => new { Channel = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).FirstOrDefault();
        if (byChannel is not null)
        {
            var pct = (int)Math.Round(100.0 * byChannel.Count / inRange.Count);
            cards.Add(new InsightCardDto
            {
                Icon = "🛎️", Title = "Tu canal más fuerte", Value = byChannel.Channel,
                Detail = $"{pct}% de tus pedidos",
            });
        }

        return cards;
    }

    /// <summary>Productos que aparecen juntos en un mismo pedido (pares más frecuentes).</summary>
    private static List<ComboPairDto> BuildCombos(List<Order> inRange)
    {
        var pairCounts = new Dictionary<(string, string), int>();
        foreach (var o in inRange)
        {
            var names = o.Items.Select(i => i.ProductName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = 0; i < names.Count; i++)
                for (var j = i + 1; j < names.Count; j++)
                {
                    var key = (names[i], names[j]);
                    pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
                }
        }

        return pairCounts
            .Where(kv => kv.Value >= 2) // al menos 2 pedidos juntos para sugerirlo
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => new ComboPairDto
            {
                ProductA = kv.Key.Item1, ProductB = kv.Key.Item2, Count = kv.Value,
                Suggestion = $"Arma un combo: {kv.Key.Item1} + {kv.Key.Item2}",
            })
            .ToList();
    }

    private static string Channel(string table) =>
        table.StartsWith("Mesa", StringComparison.OrdinalIgnoreCase) ? "Mesa"
        : table.StartsWith("Delivery", StringComparison.OrdinalIgnoreCase) ? "Delivery"
        : table.StartsWith("Para llevar", StringComparison.OrdinalIgnoreCase) ? "Para llevar"
        : string.IsNullOrWhiteSpace(table) ? "Mostrador" : table;

    private static string HourLabel(int h)
    {
        static string Ampm(int x)
        {
            var hr = x % 12; if (hr == 0) hr = 12;
            return $"{hr}{(x % 24 < 12 ? "am" : "pm")}";
        }
        return $"{Ampm(h)}–{Ampm((h + 1) % 24)}";
    }

    private static string Money(decimal v) => "$" + v.ToString("0.00");
}
