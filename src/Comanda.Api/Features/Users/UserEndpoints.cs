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

namespace Comanda.Api.Features.Users;

public sealed class ListUsersEndpoint(IUserRepository users, IRMapper mapper) : EndpointWithoutRequest<List<UserDto>>
{
    public override void Configure() => Get("/users");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await users.ListWithBranchAsync(ct);
        await Send.OkAsync(mapper.MapList<User, UserDto>(list), ct);
    }
}

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cajero;
    public Guid? BranchId { get; set; }
}

public sealed class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}

public sealed class CreateUserEndpoint(
    IUserRepository users, IPasswordHasher hasher, IPlanService planService, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<CreateUserRequest, UserDto>
{
    public override void Configure() => Post("/users");

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var allowed = await planService.EnsureCanAddUserAsync(ct);
        if (allowed.IsFailure)
        {
            await HttpContext.SendErrorAsync(allowed.Error, ct);
            return;
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await users.AnyAsync(u => u.Email == email, ct))
        {
            await HttpContext.SendErrorAsync(
                Error.Conflict("usuarios.correo_duplicado", "Ya existe un usuario con ese correo."), ct);
            return;
        }

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(req.Password),
            Role = req.Role,
            BranchId = req.BranchId,
            IsActive = true,
        };
        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<User, UserDto>(user), ct);
    }
}

public sealed class SetUserActiveRequest
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SetUserActiveEndpoint(IUserRepository users, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<SetUserActiveRequest, UserDto>
{
    public override void Configure() => Patch("/users/{id}/active");

    public override async Task HandleAsync(SetUserActiveRequest req, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(req.Id, ct);
        if (user is null)
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("usuarios.no_encontrado", "Usuario no encontrado."), ct);
            return;
        }

        user.IsActive = req.IsActive;
        users.Update(user);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<User, UserDto>(user), ct);
    }
}

public sealed class UpdateUserRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
}

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.");
    }
}

public sealed class UpdateUserEndpoint(IUserRepository users, IUnitOfWork uow, IRMapper mapper)
    : Endpoint<UpdateUserRequest, UserDto>
{
    public override void Configure() => Put("/users/{id}");

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(req.Id, ct);
        if (user is null)
        {
            await HttpContext.SendErrorAsync(
                Error.NotFound("usuarios.no_encontrado", "Usuario no encontrado."), ct);
            return;
        }

        user.Name = req.Name.Trim();
        user.Role = req.Role;
        user.BranchId = req.BranchId;
        users.Update(user);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(mapper.Map<User, UserDto>(user), ct);
    }
}
