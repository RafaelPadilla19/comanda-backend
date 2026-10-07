using System.Security.Claims;
using System.Security.Cryptography;
using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Domain.Enums;
using Comanda.Infrastructure.Security;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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
    /// <summary>Token de larga duración para renovar la sesión sin volver a pedir credenciales
    /// (ver /auth/refresh-token). Se rota en cada uso: guardar siempre el último recibido.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

/// <summary>Arma la sesión completa (JWT + refresh token persistido) — lo comparten Login,
/// Register, RefreshToken y LoginWithToken para no duplicar la lógica de emisión/rotación.</summary>
internal static class AuthSessionFactory
{
    public static async Task<LoginResponse> CreateAsync(
        User user, IJwtTokenService jwt, IRepository<RefreshToken> refreshTokens,
        IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper, CancellationToken ct)
    {
        var (token, exp) = jwt.CreateToken(user);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = GenerateOpaqueToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationDays),
        };
        await refreshTokens.AddAsync(refreshToken, ct);
        await uow.SaveChangesAsync(ct);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = exp,
            RefreshToken = refreshToken.Token,
            User = mapper.Map<User, UserDto>(user),
        };
    }

    /// <summary>Cadena aleatoria criptográficamente segura (256 bits), opaca a propósito: a
    /// diferencia del JWT no lleva claims adentro, solo sirve como llave para buscarla en BD.</summary>
    private static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
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

/// <summary>Autenticación: devuelve un JWT, un refresh token y los datos del usuario.</summary>
public sealed class LoginEndpoint(
    IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt,
    IRepository<RefreshToken> refreshTokens, IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper)
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

        var response = await AuthSessionFactory.CreateAsync(user, jwt, refreshTokens, uow, jwtOptions, mapper, ct);
        await Send.OkAsync(response, ct);
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

/// <summary>Auto-registro de un restaurante: crea el tenant, su admin y datos semilla; devuelve
/// sesión completa (mismo shape que Login, incluido el refresh token).</summary>
public sealed class RegisterEndpoint(
    ITenantProvisioningService provisioning, IJwtTokenService jwt,
    IRepository<RefreshToken> refreshTokens, IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper)
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

        var response = await AuthSessionFactory.CreateAsync(result.Value, jwt, refreshTokens, uow, jwtOptions, mapper, ct);
        await Send.OkAsync(response, ct);
    }
}

// ---------------- Renovar sesión / verificar vigencia ----------------

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RefreshTokenValidator : Validator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
        => RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Falta el refresh token.");
}

/// <summary>Canjea un refresh token vigente por una sesión nueva. Rota el refresh token (revoca
/// el usado, emite uno nuevo) — un refresh token usado dos veces falla la segunda.</summary>
public sealed class RefreshTokenEndpoint(
    IRepository<RefreshToken> refreshTokens, IUserRepository users, IJwtTokenService jwt,
    IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper)
    : Endpoint<RefreshTokenRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/refresh-token");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("login"));
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        var stored = await refreshTokens.FirstOrDefaultAsync(r => r.Token == req.RefreshToken, ct);
        if (stored is null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.refresh_invalido", "El refresh token es inválido, ya fue usado o venció. Inicia sesión de nuevo."), ct);
            return;
        }

        var user = await users.GetByIdIgnoringTenantAsync(stored.UserId, ct);
        if (user is null || !user.IsActive)
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.refresh_invalido", "El refresh token es inválido, ya fue usado o venció. Inicia sesión de nuevo."), ct);
            return;
        }

        var response = await AuthSessionFactory.CreateAsync(user, jwt, refreshTokens, uow, jwtOptions, mapper, ct);

        // Rotación: el token usado queda inutilizable, enlazado al que lo reemplazó. Si alguien
        // reusa un refresh token viejo (ej. robado de un log), esta revocación ya lo invalidó.
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByToken = response.RefreshToken;
        refreshTokens.Update(stored);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(response, ct);
    }
}

/// <summary>Confirma si el access token actual sigue vigente. Si el JWT llegó hasta acá, el
/// middleware de autenticación ya validó firma y vigencia — no hace falta repetir esa lógica.</summary>
public sealed class VerifyTokenResponse
{
    public bool IsValid { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class VerifyTokenEndpoint : EndpointWithoutRequest<VerifyTokenResponse>
{
    public override void Configure() => Get("/auth/verify-token");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId))
        {
            await HttpContext.SendErrorAsync(Error.Unauthorized("auth.token", "Token inválido."), ct);
            return;
        }

        var tenantRaw = User.FindFirstValue("tenant_id");
        Guid.TryParse(tenantRaw, out var tenantId);

        DateTime? expiresAt = null;
        var expClaim = User.FindFirstValue("exp");
        if (long.TryParse(expClaim, out var expUnix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        }

        await Send.OkAsync(new VerifyTokenResponse
        {
            IsValid = true,
            UserId = userId,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            TenantId = tenantId,
            ExpiresAt = expiresAt,
        }, ct);
    }
}

/// <summary>Opcional: si la app todavía tiene un JWT vigente, re-emite una sesión completa
/// (access + refresh token nuevos) sin pedir credenciales otra vez.</summary>
public sealed class LoginWithTokenEndpoint(
    IUserRepository users, IJwtTokenService jwt, IRepository<RefreshToken> refreshTokens,
    IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper)
    : EndpointWithoutRequest<LoginResponse>
{
    public override void Configure() => Post("/auth/login-with-token");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId))
        {
            await HttpContext.SendErrorAsync(Error.Unauthorized("auth.token", "Token inválido."), ct);
            return;
        }

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            await HttpContext.SendErrorAsync(Error.Unauthorized("auth.token", "Token inválido."), ct);
            return;
        }

        var response = await AuthSessionFactory.CreateAsync(user, jwt, refreshTokens, uow, jwtOptions, mapper, ct);
        await Send.OkAsync(response, ct);
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
        dto.TrialEndsAt = tenant?.TrialEndsAt;

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
