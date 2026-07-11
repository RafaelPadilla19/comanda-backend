using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>Cupón de descuento aplicable en la tienda o el POS.</summary>
public class Coupon : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;       // normalizado en MAYÚSCULAS
    public DiscountType Type { get; set; } = DiscountType.Percentage;
    public decimal Value { get; set; }                      // % o monto según Type
    public decimal MinOrder { get; set; }                   // subtotal mínimo (0 = sin mínimo)
    public DateTime? ExpiresAt { get; set; }                // null = sin vencimiento
    public int? MaxRedemptions { get; set; }                // null = ilimitado
    public int TimesRedeemed { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Calcula el descuento sobre un subtotal (sin exceder el subtotal).</summary>
    public decimal DiscountFor(decimal subtotal)
    {
        var raw = Type == DiscountType.Percentage ? subtotal * Value / 100m : Value;
        return Math.Min(Math.Max(0m, raw), subtotal);
    }
}
