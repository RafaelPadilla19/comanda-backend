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

namespace Comanda.Api.Features.Coupons;

// ---------------- Admin: gestión de cupones ----------------

public sealed class ListCouponsEndpoint(IRepository<Coupon> coupons, IRMapper mapper)
    : EndpointWithoutRequest<List<CouponDto>>
{
    public override void Configure() => Get("/coupons");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await coupons.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<Coupon, CouponDto>(list.OrderByDescending(c => c.CreatedAt)), ct);
    }
}

public sealed class SaveCouponRequest
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; } = DiscountType.Percentage;
    public decimal Value { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveCouponValidator : Validator<SaveCouponRequest>
{
    public SaveCouponValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código del cupón es obligatorio.");
        RuleFor(x => x.Value).GreaterThan(0).WithMessage("El valor del descuento debe ser mayor que cero.");
        RuleFor(x => x.Value).LessThanOrEqualTo(100)
            .When(x => x.Type == DiscountType.Percentage)
            .WithMessage("El porcentaje no puede ser mayor a 100.");
        RuleFor(x => x.MinOrder).GreaterThanOrEqualTo(0).WithMessage("El mínimo no puede ser negativo.");
    }
}

public sealed class CreateCouponEndpoint(
    IRepository<Coupon> coupons, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveCouponRequest, CouponDto>
{
    public override void Configure() => Post("/coupons");

    public override async Task HandleAsync(SaveCouponRequest req, CancellationToken ct)
    {
        var allowed = await planService.EnsureFeatureAsync(PlanFeature.Coupons, ct);
        if (allowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(allowed.Error, ct);
            return;
        }

        var code = req.Code.Trim().ToUpperInvariant();
        if (await coupons.AnyAsync(c => c.Code == code, ct))
        {
            await HttpContext.SendErrorAsync(Error.Conflict("cupon.duplicado", "Ya existe un cupón con ese código."), ct);
            return;
        }

        var coupon = new Coupon
        {
            Code = code, Type = req.Type, Value = req.Value, MinOrder = req.MinOrder,
            ExpiresAt = req.ExpiresAt, MaxRedemptions = req.MaxRedemptions, IsActive = req.IsActive,
        };
        await coupons.AddAsync(coupon, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Coupon, CouponDto>(coupon), ct);
    }
}

public sealed class UpdateCouponEndpoint(IRepository<Coupon> coupons, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveCouponRequest, CouponDto>
{
    public override void Configure() => Put("/coupons/{id}");

    public override async Task HandleAsync(SaveCouponRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await coupons.GetByIdAsync(id, ct) is not { } coupon)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("cupon.no_encontrado", "Cupón no encontrado."), ct);
            return;
        }

        coupon.Code = req.Code.Trim().ToUpperInvariant();
        coupon.Type = req.Type;
        coupon.Value = req.Value;
        coupon.MinOrder = req.MinOrder;
        coupon.ExpiresAt = req.ExpiresAt;
        coupon.MaxRedemptions = req.MaxRedemptions;
        coupon.IsActive = req.IsActive;
        coupons.Update(coupon);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Coupon, CouponDto>(coupon), ct);
    }
}

public sealed class DeleteCouponRequest { public Guid Id { get; set; } }

public sealed class DeleteCouponEndpoint(IRepository<Coupon> coupons, IUnitOfWork uow)
    : Endpoint<DeleteCouponRequest>
{
    public override void Configure() => Delete("/coupons/{id}");

    public override async Task HandleAsync(DeleteCouponRequest req, CancellationToken ct)
    {
        if (await coupons.GetByIdAsync(req.Id, ct) is { } coupon)
        {
            coupons.Remove(coupon);
            await uow.SaveChangesAsync(ct);
        }
        await Send.NoContentAsync(ct);
    }
}

// ---------------- Público: validar cupón en el checkout ----------------

public sealed class ValidateCouponRequest
{
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
}

public sealed class ValidateCouponEndpoint(ITenantResolver resolver, ICouponService coupons)
    : Endpoint<ValidateCouponRequest, CouponValidationDto>
{
    public override void Configure()
    {
        Post("/public/coupons/validate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ValidateCouponRequest req, CancellationToken ct)
    {
        if (!await resolver.ResolveByBranchAsync(req.BranchId, ct))
        {
            await Send.OkAsync(new CouponValidationDto { Valid = false, Message = "Sucursal no válida." }, ct);
            return;
        }

        var result = await coupons.ApplyAsync(req.Code, req.Subtotal, ct);
        await Send.OkAsync(result.IsSuccess
            ? new CouponValidationDto { Valid = true, Code = result.Value.Coupon.Code, Discount = result.Value.Discount, Message = "Cupón aplicado." }
            : new CouponValidationDto { Valid = false, Message = result.Error.Message }, ct);
    }
}
