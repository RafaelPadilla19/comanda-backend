using Comanda.Domain.Common;
using Comanda.Domain.Entities;

namespace Comanda.Domain.Abstractions;

/// <summary>Cupón validado y el descuento que aplica a un subtotal.</summary>
public sealed record CouponApplication(Coupon Coupon, decimal Discount);

/// <summary>
/// Valida un cupón del tenant actual contra un subtotal y calcula el descuento.
/// Devuelve fallo (con mensaje en español) si no existe, está inactivo, vencido,
/// agotado o no alcanza el mínimo.
/// </summary>
public interface ICouponService
{
    Task<Result<CouponApplication>> ApplyAsync(string code, decimal itemsSubtotal, CancellationToken ct = default);
}
