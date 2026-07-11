namespace Comanda.Domain.Abstractions;

/// <summary>
/// Resuelve y fija el tenant actual en contextos sin JWT (tienda pública), a partir
/// de identificadores globalmente únicos (sucursal o slug del restaurante).
/// </summary>
public interface ITenantResolver
{
    /// <summary>Fija el tenant a partir del Id de una sucursal. False si no existe.</summary>
    Task<bool> ResolveByBranchAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>Fija el tenant a partir del slug del restaurante. False si no existe o está inactivo.</summary>
    Task<bool> ResolveBySlugAsync(string slug, CancellationToken ct = default);
}
