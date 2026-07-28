using System.Security.Claims;
using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Auth;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

/// <summary>Autenticación: devuelve un JWT y los datos del usuario.</summary>
public sealed class LoginEndpoint(
    IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt, IRMapper mapper)
    : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("login"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, ct);

        if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.credenciales", "Correo o contraseña incorrectos."), ct);
            return;
        }
        if (!user.IsActive)
        {
            await HttpContext.SendErrorAsync(
                Error.Forbidden("auth.inactivo", "Tu usuario está inactivo. Contacta al administrador."), ct);
            return;
        }
        if (user.Tenant is { SubscriptionStatus: SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled })
        {
            await HttpContext.SendErrorAsync(
                Error.Forbidden("auth.suspendido", "La cuenta del restaurante está suspendida. Contacta a soporte."), ct);
            return;
        }

        var (token, exp) = jwt.CreateToken(user);
        await Send.OkAsync(new LoginResponse
        {
            Token = token,
            ExpiresAt = exp,
            User = mapper.Map<User, UserDto>(user),
        }, ct);
    }
}

public sealed class RegisterRequest
{
    public string RestaurantName { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.RestaurantName).NotEmpty().WithMessage("El nombre del restaurante es obligatorio.");
        RuleFor(x => x.AdminName).NotEmpty().WithMessage("Tu nombre es obligatorio.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
        RuleFor(x => x.Country).NotEmpty().WithMessage("Selecciona tu país.")
            .Must(c => Comanda.Api.Features.Settings.Countries.IsValid((c ?? string.Empty).Trim().ToUpperInvariant()))
            .WithMessage("País no soportado.");
    }
}

/// <summary>Auto-registro de un restaurante: crea el tenant, su admin y datos semilla; devuelve el JWT.</summary>
public sealed class RegisterEndpoint(
    ITenantProvisioningService provisioning, IJwtTokenService jwt, IRMapper mapper)
    : Endpoint<RegisterRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var result = await provisioning.RegisterAsync(req.RestaurantName, req.AdminName, req.Email, req.Password, req.Country, ct);
        if (result.IsFailure)
        {
            await HttpContext.SendErrorAsync(result.Error, ct);
            return;
        }

        var (token, exp) = jwt.CreateToken(result.Value);
        await Send.OkAsync(new LoginResponse
        {
            Token = token,
            ExpiresAt = exp,
            User = mapper.Map<User, UserDto>(result.Value),
        }, ct);
    }
}

/// <summary>Devuelve el usuario autenticado a partir del token (incluye las funciones de su plan).</summary>
public sealed class MeEndpoint(IUserRepository users, IRepository<Tenant> tenants, IPlanService planService, IRMapper mapper)
    : EndpointWithoutRequest<UserDto>
{
    public override void Configure() => Get("/auth/me");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var id))
        {
            await HttpContext.SendErrorAsync(Error.Unauthorized("auth.token", "Token inválido."), ct);
            return;
        }

        var user = await users.GetByIdAsync(id, ct);
        if (user is null)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("auth.usuario", "Usuario no encontrado."), ct);
            return;
        }

        var dto = mapper.Map<User, UserDto>(user);
        var tenant = await tenants.GetByIdAsync(user.TenantId, ct);
        dto.TenantName = tenant?.Name ?? string.Empty;
        dto.SubscriptionStatus = tenant?.SubscriptionStatus ?? SubscriptionStatus.Active;
        dto.SubscriptionEndsAt = tenant?.SubscriptionEndsAt;

        var usage = await planService.GetUsageAsync(ct);
        if (usage.HasPlan)
        {
            dto.Plan = new PlanFeaturesDto
            {
                PlanName = usage.PlanName,
                OnlinePayments = usage.OnlinePayments, Coupons = usage.Coupons, Loyalty = usage.Loyalty,
                AdvancedReports = usage.AdvancedReports, Inventory = usage.Inventory,
            };
        }
        await Send.OkAsync(dto, ct);
    }
}
