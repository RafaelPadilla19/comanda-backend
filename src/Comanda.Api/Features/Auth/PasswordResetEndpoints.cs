using System.Security.Cryptography;
using System.Text;
using Comanda.Api.Common;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using Comanda.Infrastructure.Security;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RMapper.Core.Interfaces;

namespace Comanda.Api.Features.Auth;

/// <summary>Helpers compartidos del flujo de "olvidé mi contraseña" (OTP → token → reset).</summary>
internal static class PasswordResetTokens
{
    public static string GenerateOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Token opaco de un solo uso (igual mecanismo que RefreshToken) — no es el OTP,
    /// así un OTP ya canjeado no sirve para pedir otro reset.</summary>
    public static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

// ---------------- 1. Solicitar OTP ----------------

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ForgotPasswordValidator : Validator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
    }
}

/// <summary>Genera un OTP de 6 dígitos y lo envía por correo si el email pertenece a un
/// usuario. Responde siempre el mismo mensaje exista o no la cuenta, para no filtrar qué
/// correos están registrados.</summary>
public sealed class ForgotPasswordEndpoint(
    IUserRepository users, IRepository<PasswordResetRequest> resets,
    IEmailSender email, IUnitOfWork uow)
    : Endpoint<ForgotPasswordRequest>
{
    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("password-reset"));
    }

    public override async Task HandleAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, ct);

        if (user is not null && user.IsActive)
        {
            var otp = PasswordResetTokens.GenerateOtp();
            var request = new PasswordResetRequest
            {
                Email = normalizedEmail,
                OtpHash = PasswordResetTokens.Hash(otp),
                OtpExpiresAt = DateTime.UtcNow.AddMinutes(10),
            };
            await resets.AddAsync(request, ct);
            await uow.SaveChangesAsync(ct);

            try
            {
                await email.SendAsync(normalizedEmail, "Código para restablecer tu contraseña - Comanda", $"""
                    <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:20px">
                        <div style="background:linear-gradient(135deg,#ea580c,#f97316);color:#fff;padding:20px;text-align:center;border-radius:8px 8px 0 0">
                            <h2 style="margin:0">Comanda</h2>
                        </div>
                        <div style="background:#f9fafb;padding:30px;border:1px solid #e5e7eb;text-align:center">
                            <p>Tu código para restablecer la contraseña es:</p>
                            <div style="font-size:32px;font-weight:bold;letter-spacing:8px;color:#ea580c;background:#fff7ed;padding:15px;border-radius:8px;margin:20px 0">{otp}</div>
                            <p style="color:#6b7280;font-size:14px">Este código expira en 10 minutos.</p>
                            <p style="color:#6b7280;font-size:12px">Si no solicitaste restablecer tu contraseña, ignora este correo.</p>
                        </div>
                    </div>
                    """, ct);
            }
            catch
            {
                // No se revela la falla de envío al cliente: mismo mensaje genérico de abajo.
            }
        }

        await Send.OkAsync(new
        {
            mensaje = "Si el correo está registrado, recibirás un código de verificación.",
        }, cancellation: ct);
    }
}

// ---------------- 2. Verificar OTP ----------------

public sealed class VerifyOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class VerifyOtpResponse
{
    public string Token { get; set; } = string.Empty;
}

public sealed class VerifyOtpValidator : Validator<VerifyOtpRequest>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.");
    }
}

/// <summary>Canjea el OTP vigente por un token de un solo uso (válido 15 minutos), que
/// ResetPasswordEndpoint exige para completar el cambio de contraseña.</summary>
public sealed class VerifyOtpEndpoint(IRepository<PasswordResetRequest> resets, IUnitOfWork uow)
    : Endpoint<VerifyOtpRequest, VerifyOtpResponse>
{
    private const int MaxAttempts = 5;

    public override void Configure()
    {
        Post("/auth/verify-otp");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("password-reset"));
    }

    public override async Task HandleAsync(VerifyOtpRequest req, CancellationToken ct)
    {
        var normalizedEmail = req.Email.Trim().ToLowerInvariant();
        var pending = (await resets.ListAsync(
                r => r.Email == normalizedEmail && !r.Used && r.ResetToken == null, ct))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        if (pending is null || pending.OtpExpiresAt <= DateTime.UtcNow || pending.Attempts >= MaxAttempts)
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.otp_invalido", "El código es inválido o expiró. Solicita uno nuevo."), ct);
            return;
        }

        pending.Attempts++;

        if (pending.OtpHash != PasswordResetTokens.Hash(req.Code.Trim()))
        {
            resets.Update(pending);
            await uow.SaveChangesAsync(ct);
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.otp_invalido", "El código es inválido o expiró. Solicita uno nuevo."), ct);
            return;
        }

        pending.ResetToken = PasswordResetTokens.GenerateOpaqueToken();
        pending.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
        resets.Update(pending);
        await uow.SaveChangesAsync(ct);

        await Send.OkAsync(new VerifyOtpResponse { Token = pending.ResetToken }, ct);
    }
}

// ---------------- 3. Restablecer contraseña con el token ----------------

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Falta el token de verificación.");
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.");
    }
}

/// <summary>Aplica la nueva contraseña usando el token emitido por VerifyOtpEndpoint y
/// devuelve una sesión completa — mismo shape que /auth/login.</summary>
public sealed class ResetPasswordEndpoint(
    IUserRepository users, IRepository<PasswordResetRequest> resets,
    IPasswordHasher hasher, IJwtTokenService jwt, IRepository<RefreshToken> refreshTokens,
    IUnitOfWork uow, IOptions<JwtOptions> jwtOptions, IRMapper mapper)
    : Endpoint<ResetPasswordRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();
        Options(b => b.RequireRateLimiting("password-reset"));
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var pending = await resets.FirstOrDefaultAsync(r => r.ResetToken == req.Token, ct);
        if (pending is null || pending.Used || pending.ResetTokenExpiresAt is null || pending.ResetTokenExpiresAt <= DateTime.UtcNow)
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.reset_token_invalido", "El enlace de restablecimiento es inválido o expiró. Solicita uno nuevo."), ct);
            return;
        }

        var user = await users.GetByEmailAsync(pending.Email, ct);
        if (user is null || !user.IsActive)
        {
            await HttpContext.SendErrorAsync(
                Error.Unauthorized("auth.reset_token_invalido", "El enlace de restablecimiento es inválido o expiró. Solicita uno nuevo."), ct);
            return;
        }

        user.PasswordHash = hasher.Hash(req.NewPassword);
        users.Update(user);

        pending.Used = true;
        resets.Update(pending);
        await uow.SaveChangesAsync(ct);

        var response = await AuthSessionFactory.CreateAsync(user, jwt, refreshTokens, uow, jwtOptions, mapper, ct);
        await Send.OkAsync(response, ct);
    }
}
