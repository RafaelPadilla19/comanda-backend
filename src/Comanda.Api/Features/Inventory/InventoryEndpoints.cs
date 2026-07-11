using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Inventory;

public sealed class ListInventoryEndpoint(IRepository<InventoryItem> items, IRMapper mapper)
    : EndpointWithoutRequest<List<InventoryItemDto>>
{
    public override void Configure() => Get("/inventory");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await items.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<InventoryItem, InventoryItemDto>(list.OrderBy(i => i.Name)), ct);
    }
}

public sealed class CreateInventoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Min { get; set; }
    public decimal Cost { get; set; }
}

public sealed class CreateInventoryValidator : Validator<CreateInventoryRequest>
{
    public CreateInventoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del insumo es obligatorio.");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("La unidad de medida es obligatoria.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0).WithMessage("El stock no puede ser negativo.");
        RuleFor(x => x.Min).GreaterThanOrEqualTo(0).WithMessage("El mínimo no puede ser negativo.");
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).WithMessage("El costo no puede ser negativo.");
    }
}

public sealed class CreateInventoryEndpoint(
    IRepository<InventoryItem> items, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<CreateInventoryRequest, InventoryItemDto>
{
    public override void Configure() => Post("/inventory");

    public override async Task HandleAsync(CreateInventoryRequest req, CancellationToken ct)
    {
        var allowed = await planService.EnsureFeatureAsync(PlanFeature.Inventory, ct);
        if (allowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(allowed.Error, ct);
            return;
        }

        var item = new InventoryItem
        {
            Name = req.Name.Trim(), Category = req.Category.Trim(),
            Stock = req.Stock, Unit = req.Unit.Trim(), Min = req.Min, Cost = req.Cost,
        };
        await items.AddAsync(item, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<InventoryItem, InventoryItemDto>(item), ct);
    }
}

public sealed class AdjustStockRequest
{
    public Guid Id { get; set; }
    public decimal Delta { get; set; } // positivo = entrada, negativo = salida
}

public sealed class AdjustStockEndpoint(IRepository<InventoryItem> items, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<AdjustStockRequest, InventoryItemDto>
{
    public override void Configure() => Post("/inventory/{id}/adjust");

    public override async Task HandleAsync(AdjustStockRequest req, CancellationToken ct)
    {
        if (await items.GetByIdAsync(req.Id, ct) is not { } item)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("inventario.no_encontrado", "Insumo no encontrado."), ct);
            return;
        }

        item.Stock = Math.Max(0, Math.Round(item.Stock + req.Delta, 3));
        items.Update(item);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<InventoryItem, InventoryItemDto>(item), ct);
    }
}
