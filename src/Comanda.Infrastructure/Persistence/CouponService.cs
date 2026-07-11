using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Comanda.Infrastructure.Persistence;

public sealed class CouponService(ComandaDbContext db) : ICouponService
{
    public async Task<Result<CouponApplication>> ApplyAsync(string code, decimal itemsSubtotal, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        // El filtro global de tenant aplica: solo encuentra cupones del tenant actual.
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized, ct);

        if (coupon is null || !coupon.IsActive)
            return Error.Validation("cupon.invalido", "El cupón no es válido.");

        if (coupon.ExpiresAt is { } exp && exp < DateTime.UtcNow)
            return Error.Validation("cupon.vencido", "El cupón ya venció.");

        if (coupon.MaxRedemptions is { } max && coupon.TimesRedeemed >= max)
            return Error.Validation("cupon.agotado", "El cupón ya no está disponible.");

        if (itemsSubtotal < coupon.MinOrder)
            return Error.Validation("cupon.minimo",
                $"Este cupón requiere un pedido mínimo de ${coupon.MinOrder:0.00}.");

        return new CouponApplication(coupon, coupon.DiscountFor(itemsSubtotal));
    }
}
