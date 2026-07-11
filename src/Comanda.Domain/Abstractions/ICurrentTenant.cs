namespace Comanda.Domain.Abstractions;

/// <summary>
/// Contexto del tenant actual de la petición. Lo fija el middleware (desde el JWT)
/// o los endpoints públicos (resolviendo la sucursal). El DbContext lo usa para el
/// filtro global y el estampado de TenantId.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
    bool HasTenant { get; }
    void Set(Guid tenantId);
}
