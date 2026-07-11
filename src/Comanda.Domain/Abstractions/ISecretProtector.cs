namespace Comanda.Domain.Abstractions;

/// <summary>Cifra/descifra secretos (p.ej. el ApiSecret de Wompi del tenant) para guardarlos en reposo.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
