namespace Comanda.Domain.Common;

/// <summary>
/// Marca una entidad como perteneciente a un tenant (restaurante). El TenantId se
/// estampa automáticamente al insertar y se filtra en cada consulta (multi-tenant).
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
