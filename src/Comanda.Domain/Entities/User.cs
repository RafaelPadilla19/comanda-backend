using Comanda.Domain.Common;
using Comanda.Domain.Enums;

namespace Comanda.Domain.Entities;

/// <summary>Usuario del sistema. <see cref="BranchId"/> nulo = todas las sucursales.</summary>
public class User : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cajero;
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
}
