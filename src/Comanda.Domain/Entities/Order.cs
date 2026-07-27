using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>Pedido (comanda) con sus líneas.</summary>
public class Order : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;       // p.ej. "#1042"
    public string Table { get; set; } = string.Empty;       // "Mesa 4", "Para llevar", "Delivery"
    public OrderStatus Status { get; set; } = OrderStatus.Nuevos;
    public OrderChannel Channel { get; set; } = OrderChannel.Local;
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public Guid? CustomerId { get; set; }   // CRM: cliente asociado (pedidos online)

    // Datos del cliente (los llena la tienda pública / QR; en POS quedan vacíos).
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty; // solo Delivery
    public string Notes { get; set; } = string.Empty;

    // Delivery
    public decimal DeliveryFee { get; set; }                // tarifa de envío (solo Delivery)
    public string DeliveryZoneName { get; set; } = string.Empty; // snapshot de la zona elegida
    public Guid? DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;  // repartidor asignado
    public DateTime? DispatchedAt { get; set; }             // marca "en camino"

    // Rider externo (pool de RidersHub), cuando no hay repartidor propio disponible
    public Guid? RiderJobId { get; set; }                   // id del job en RidersHub
    public string RiderJobStatus { get; set; } = string.Empty; // Open/Accepted/Delivered (snapshot)

    // Descuento / cupón
    public string CouponCode { get; set; } = string.Empty;  // código aplicado (vacío = ninguno)
    public decimal DiscountAmount { get; set; }             // monto descontado (cupón + puntos)

    // Fidelización
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }

    // Pago en línea (vía PaymentsHub)
    public bool IsPaid { get; set; }
    public string PaymentRef { get; set; } = string.Empty;  // id del cobro en PaymentsHub

    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();

    /// <summary>Subtotal de las líneas (sin envío ni descuento).</summary>
    public decimal ItemsSubtotal() => Items.Sum(i => i.UnitPrice * i.Quantity);

    /// <summary>Recalcula el total: líneas + envío − descuento (nunca negativo).</summary>
    public void RecalculateTotal() => Total = Math.Max(0m, ItemsSubtotal() + DeliveryFee - DiscountAmount);
}

/// <summary>Línea de un pedido.</summary>
public class OrderItem : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    /// <summary>Resumen legible de variante + extras elegidos, p.ej. "Maíz · +Queso extra".</summary>
    public string Modifiers { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
