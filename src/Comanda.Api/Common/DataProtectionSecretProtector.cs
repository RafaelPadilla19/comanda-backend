using Comanda.Domain.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Comanda.Api.Common;

/// <summary>Implementa el cifrado de secretos con ASP.NET Data Protection.</summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Comanda.TenantSecrets.v1");

    public string Protect(string plaintext) => string.IsNullOrEmpty(plaintext) ? string.Empty : _protector.Protect(plaintext);

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        try { return _protector.Unprotect(ciphertext); }
        catch { return string.Empty; } // clave rotada o dato corrupto → trátalo como "sin secreto"
    }
}
