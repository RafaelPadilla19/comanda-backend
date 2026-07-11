using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Caja;

public sealed class GetCurrentCajaEndpoint(ICashSessionRepository sessions, IRMapper mapper)
    : EndpointWithoutRequest<CashSessionDto>
{
    public override void Configure() => Get("/caja/current");

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await sessions.GetOpenAsync(ct) is not { } session)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("caja.sin_apertura", "No hay una caja abierta."), ct);
            return;
        }
        await Send.OkAsync(mapper.Map<CashSession, CashSessionDto>(session), ct);
    }
}

public sealed class OpenCajaRequest
{
    public Guid? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Cashier { get; set; } = string.Empty;
    public decimal OpeningFund { get; set; }
}

public sealed class OpenCajaValidator : Validator<OpenCajaRequest>
{
    public OpenCajaValidator()
    {
        RuleFor(x => x.Cashier).NotEmpty().WithMessage("Indica el cajero responsable.");
        RuleFor(x => x.OpeningFund).GreaterThanOrEqualTo(0).WithMessage("El fondo inicial no puede ser negativo.");
    }
}

public sealed class OpenCajaEndpoint(ICashSessionRepository sessions, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<OpenCajaRequest, CashSessionDto>
{
    public override void Configure() => Post("/caja/open");

    public override async Task HandleAsync(OpenCajaRequest req, CancellationToken ct)
    {
        if (await sessions.GetOpenAsync(ct) is not null)
        {
            await HttpContext.SendErrorAsync(Error.Conflict("caja.ya_abierta", "Ya hay una caja abierta."), ct);
            return;
        }

        var session = new CashSession
        {
            BranchId = req.BranchId,
            BranchName = req.BranchName.Trim(),
            CashierName = req.Cashier.Trim(),
            IsOpen = true,
            OpenedAt = DateTime.UtcNow,
        };
        if (req.OpeningFund > 0)
            session.Movements.Add(new CashMovement
            {
                Label = "Apertura de caja", Sub = "Fondo inicial",
                Amount = req.OpeningFund, Type = MovementType.Fondo,
            });

        await sessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<CashSession, CashSessionDto>(session), ct);
    }
}

public sealed class CloseCajaEndpoint(ICashSessionRepository sessions, IUnitOfWork uow, IRMapper mapper)
    : EndpointWithoutRequest<CashSessionDto>
{
    public override void Configure() => Post("/caja/close");

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (await sessions.GetOpenAsync(ct) is not { } session)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("caja.sin_apertura", "No hay una caja abierta para cerrar."), ct);
            return;
        }

        session.IsOpen = false;
        session.ClosedAt = DateTime.UtcNow;
        sessions.Update(session);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<CashSession, CashSessionDto>(session), ct);
    }
}

public sealed class AddMovementRequest
{
    public string Label { get; set; } = string.Empty;
    public string Sub { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public MovementType Type { get; set; }
}

public sealed class AddMovementValidator : Validator<AddMovementRequest>
{
    public AddMovementValidator()
    {
        RuleFor(x => x.Label).NotEmpty().WithMessage("La descripción del movimiento es obligatoria.");
        RuleFor(x => x.Amount).NotEqual(0).WithMessage("El monto no puede ser cero.");
    }
}

public sealed class AddMovementEndpoint(
    ICashSessionRepository sessions, IRepository<CashMovement> movements, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<AddMovementRequest, CashSessionDto>
{
    public override void Configure() => Post("/caja/movements");

    public override async Task HandleAsync(AddMovementRequest req, CancellationToken ct)
    {
        var session = await sessions.GetOpenAsync(ct);
        if (session is null)
        {
            await HttpContext.SendErrorAsync(Error.Conflict("caja.sin_apertura", "Abre la caja antes de registrar movimientos."), ct);
            return;
        }

        // Normaliza el signo según el tipo: egreso siempre negativo; ingreso/fondo positivo.
        var magnitude = Math.Abs(req.Amount);
        var amount = req.Type == MovementType.Egreso ? -magnitude : magnitude;

        // Inserta el movimiento como entidad nueva e independiente (Added) en vez
        // de mutar el grafo cargado de la sesión, evitando conflictos de tracking.
        await movements.AddAsync(new CashMovement
        {
            CashSessionId = session.Id,
            Label = req.Label.Trim(),
            Sub = req.Sub.Trim(),
            Amount = amount,
            Type = req.Type,
        }, ct);
        await uow.SaveChangesAsync(ct);

        var refreshed = await sessions.GetWithMovementsAsync(session.Id, ct) ?? session;
        await Send.OkAsync(mapper.Map<CashSession, CashSessionDto>(refreshed), ct);
    }
}
