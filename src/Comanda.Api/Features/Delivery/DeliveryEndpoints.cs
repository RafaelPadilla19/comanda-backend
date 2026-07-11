using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Delivery;

// ============================================================
//  Delivery autogestionado: zonas con tarifa + repartidores.
// ============================================================

// ---------- Zonas de entrega ----------

public sealed class ListZonesEndpoint(IRepository<DeliveryZone> zones, IRMapper mapper)
    : EndpointWithoutRequest<List<DeliveryZoneDto>>
{
    public override void Configure() => Get("/delivery/zones");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await zones.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<DeliveryZone, DeliveryZoneDto>(list.OrderBy(z => z.Name)), ct);
    }
}

public sealed class SaveZoneRequest
{
    public Guid? Id { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveZoneValidator : Validator<SaveZoneRequest>
{
    public SaveZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre de la zona es obligatorio.");
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0).WithMessage("La tarifa no puede ser negativa.");
    }
}

public sealed class CreateZoneEndpoint(IRepository<DeliveryZone> zones, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveZoneRequest, DeliveryZoneDto>
{
    public override void Configure() => Post("/delivery/zones");

    public override async Task HandleAsync(SaveZoneRequest req, CancellationToken ct)
    {
        var zone = new DeliveryZone { BranchId = req.BranchId, Name = req.Name.Trim(), Fee = req.Fee, IsActive = req.IsActive };
        await zones.AddAsync(zone, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<DeliveryZone, DeliveryZoneDto>(zone), ct);
    }
}

public sealed class UpdateZoneEndpoint(IRepository<DeliveryZone> zones, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveZoneRequest, DeliveryZoneDto>
{
    public override void Configure() => Put("/delivery/zones/{id}");

    public override async Task HandleAsync(SaveZoneRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await zones.GetByIdAsync(id, ct) is not { } zone)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("delivery.zona_no_encontrada", "Zona no encontrada."), ct);
            return;
        }
        zone.BranchId = req.BranchId;
        zone.Name = req.Name.Trim();
        zone.Fee = req.Fee;
        zone.IsActive = req.IsActive;
        zones.Update(zone);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<DeliveryZone, DeliveryZoneDto>(zone), ct);
    }
}

public sealed class DeleteZoneRequest { public Guid Id { get; set; } }

public sealed class DeleteZoneEndpoint(IRepository<DeliveryZone> zones, IUnitOfWork uow)
    : Endpoint<DeleteZoneRequest>
{
    public override void Configure() => Delete("/delivery/zones/{id}");

    public override async Task HandleAsync(DeleteZoneRequest req, CancellationToken ct)
    {
        if (await zones.GetByIdAsync(req.Id, ct) is { } zone)
        {
            zones.Remove(zone);
            await uow.SaveChangesAsync(ct);
        }
        await Send.NoContentAsync(ct);
    }
}

// ---------- Repartidores ----------

public sealed class ListDriversEndpoint(IRepository<Driver> drivers, IRMapper mapper)
    : EndpointWithoutRequest<List<DriverDto>>
{
    public override void Configure() => Get("/delivery/drivers");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await drivers.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<Driver, DriverDto>(list.OrderBy(d => d.Name)), ct);
    }
}

public sealed class SaveDriverRequest
{
    public Guid? Id { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SaveDriverValidator : Validator<SaveDriverRequest>
{
    public SaveDriverValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del repartidor es obligatorio.");
}

public sealed class CreateDriverEndpoint(IRepository<Driver> drivers, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveDriverRequest, DriverDto>
{
    public override void Configure() => Post("/delivery/drivers");

    public override async Task HandleAsync(SaveDriverRequest req, CancellationToken ct)
    {
        var driver = new Driver { BranchId = req.BranchId, Name = req.Name.Trim(), Phone = req.Phone.Trim(), IsActive = req.IsActive };
        await drivers.AddAsync(driver, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Driver, DriverDto>(driver), ct);
    }
}

public sealed class UpdateDriverEndpoint(IRepository<Driver> drivers, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveDriverRequest, DriverDto>
{
    public override void Configure() => Put("/delivery/drivers/{id}");

    public override async Task HandleAsync(SaveDriverRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await drivers.GetByIdAsync(id, ct) is not { } driver)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("delivery.repartidor_no_encontrado", "Repartidor no encontrado."), ct);
            return;
        }
        driver.BranchId = req.BranchId;
        driver.Name = req.Name.Trim();
        driver.Phone = req.Phone.Trim();
        driver.IsActive = req.IsActive;
        drivers.Update(driver);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Driver, DriverDto>(driver), ct);
    }
}
