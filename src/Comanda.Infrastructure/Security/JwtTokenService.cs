using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Comanda.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("tenant_id", user.TenantId.ToString()),
        };
        if (user.BranchId is { } branchId)
            claims.Add(new Claim("branchId", branchId.ToString()));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string Token, DateTime ExpiresAtUtc) CreatePlatformToken(PlatformAdmin admin)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email),
            new(ClaimTypes.Name, admin.Name),
            new(ClaimTypes.Role, "PlatformAdmin"),   // NO lleva tenant_id
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer, audience: _opt.Audience, claims: claims,
            notBefore: DateTime.UtcNow, expires: expires, signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
