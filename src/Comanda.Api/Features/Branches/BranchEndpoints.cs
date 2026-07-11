using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using FluentValidation;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Branches;

public sealed class ListBranchesEndpoint(IRepository<Branch> branches, IRMapper mapper)
    : EndpointWithoutRequest<List<BranchDto>>
{
    public override void Configure() => Get("/branches");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await branches.ListAsync(ct);
        await Send.OkAsync(mapper.MapList<Branch, BranchDto>(list.OrderBy(b => b.Name)), ct);
    }
}

public sealed class SaveBranchRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    public string WhatsappPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SaveBranchValidator : Validator<SaveBranchRequest>
{
    public SaveBranchValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre de la sucursal es obligatorio.");
        RuleFor(x => x.Address).NotEmpty().WithMessage("La dirección es obligatoria.");
    }
}

public sealed class CreateBranchEndpoint(IRepository<Branch> branches, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveBranchRequest, BranchDto>
{
    public override void Configure() => Post("/branches");

    public override async Task HandleAsync(SaveBranchRequest req, CancellationToken ct)
    {
        var allowed = await planService.EnsureCanAddBranchAsync(ct);
        if (allowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(allowed.Error, ct);
            return;
        }

        var branch = new Branch { Name = req.Name.Trim(), Address = req.Address.Trim(), Hours = req.Hours.Trim(), WhatsappPhone = BranchHelpers.DigitsOnly(req.WhatsappPhone), IsActive = req.IsActive };
        await branches.AddAsync(branch, ct);
        await uow.SaveChangesAsync(ct);
        await Send.OkAsync(mapper.Map<Branch, BranchDto>(branch), ct);
    }
}

public sealed class UpdateBranchEndpoint(IRepository<Branch> branches, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SaveBranchRequest, BranchDto>
{
    public override void Configure() => Put("/branches/{id}");

    public override async Task HandleAsync(SaveBranchRequest req, CancellationToken ct)
    {
        if (req.Id is not { } id || await branches.GetByIdAsync(id, ct) is not { } branch)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("sucursales.no_encontrada", "Sucursal no encontrada."), ct);
            return;
        }

        branch.Name = req.Name.Trim();
        branch.Address = req.Address.Trim();
        branch.Hours = req.Hours.Trim();
        branch.WhatsappPhone = BranchHelpers.DigitsOnly(req.WhatsappPhone);
        branch.IsActive = req.IsActive;
        branches.Update(branch);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<Branch, BranchDto>(branch), ct);
    }
}

internal static class BranchHelpers
{
    /// <summary>Deja solo dígitos en el teléfono (formato requerido por wa.me).</summary>
    public static string DigitsOnly(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? string.Empty : new string(phone.Where(char.IsDigit).ToArray());
}
