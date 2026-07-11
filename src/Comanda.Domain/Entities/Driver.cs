using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Repartidor (motorista) del restaurante para pedidos a domicilio.</summary>
public class Driver : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    /// <summary>Sucursal a la que pertenece. Null = disponible para todas.</summary>
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}
