using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Settings;

// ---------------- Impresoras ----------------

public sealed class ListPrintersEndpoint(IRepository<Printer> printers, IRMapper mapper)
    : EndpointWithoutRequest<List<PrinterDto>>
{
    public override void Configure() => Get("/settings/printers");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await printers.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<Printer, PrinterDto>(list.OrderBy(p => p.Name)), ct);
    }
}

public sealed class TogglePrinterRequest
{
    public string Key { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}

public sealed class TogglePrinterEndpoint(IRepository<Printer> printers, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<TogglePrinterRequest, PrinterDto>
{
    public override void Configure() => Patch("/settings/printers/{key}");

    public override async Task HandleAsync(TogglePrinterRequest req, CancellationToken ct)
    {
        if (await printers.FirstOrDefaultAsync(p => p.Key == req.Key, ct) is not { } printer)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("impresoras.no_encontrada", "Impresora no encontrada."), ct);
            return;
        }

        printer.IsConnected = req.IsConnected;
        printers.Update(printer);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Printer, PrinterDto>(printer), ct);
    }
}

public sealed class CreatePrinterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Use { get; set; } = string.Empty;
    public string Connection { get; set; } = "USB";
}

public sealed class CreatePrinterValidator : Validator<CreatePrinterRequest>
{
    public CreatePrinterValidator()
        => RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre de la impresora es obligatorio.");
}

public sealed class CreatePrinterEndpoint(IRepository<Printer> printers, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<CreatePrinterRequest, PrinterDto>
{
    public override void Configure() => Post("/settings/printers");

    public override async Task HandleAsync(CreatePrinterRequest req, CancellationToken ct)
    {
        // Clave estable derivada del nombre (slug) más un sufijo aleatorio corto.
        var slug = new string(req.Name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var printer = new Printer
        {
            Key = $"pr-{slug}-{Guid.NewGuid().ToString()[..4]}",
            Name = req.Name.Trim(), Model = req.Model.Trim(), Use = req.Use.Trim(),
            Connection = string.IsNullOrWhiteSpace(req.Connection) ? "USB" : req.Connection.Trim(),
            IsConnected = false,
        };
        await printers.AddAsync(printer, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Printer, PrinterDto>(printer), ct);
    }
}

public sealed class TestPrinterRequest
{
    public string Key { get; set; } = string.Empty;
}

public sealed class TestPrinterResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Simula una impresión de prueba: ok si la impresora está conectada.</summary>
public sealed class TestPrinterEndpoint(IRepository<Printer> printers)
    : Endpoint<TestPrinterRequest, TestPrinterResult>
{
    public override void Configure() => Post("/settings/printers/{key}/test");

    public override async Task HandleAsync(TestPrinterRequest req, CancellationToken ct)
    {
        if (await printers.FirstOrDefaultAsync(p => p.Key == req.Key, ct) is not { } printer)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("impresoras.no_encontrada", "Impresora no encontrada."), ct);
            return;
        }

        await Send.OkAsync(new TestPrinterResult
        {
            Ok = printer.IsConnected,
            Message = printer.IsConnected
                ? $"Página de prueba enviada a «{printer.Name}»."
                : $"«{printer.Name}» está desconectada. Actívala antes de imprimir.",
        }, ct);
    }
}
