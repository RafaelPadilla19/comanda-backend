using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>
/// Cliente del restaurante (CRM). Se crea/actualiza automáticamente a partir de los
/// pedidos online; la clave natural es el teléfono normalizado (solo dígitos).
/// </summary>
public class Customer : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;       // normalizado: solo dígitos
    public string Address { get; set; } = string.Empty;     // última dirección conocida
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public int Points { get; set; }              // saldo de puntos de fidelización
    public DateTime FirstOrderAt { get; set; }
    public DateTime LastOrderAt { get; set; }

    /// <summary>Registra un pedido del cliente actualizando sus estadísticas.</summary>
    public void RegisterOrder(decimal amount, DateTime when)
    {
        if (OrderCount == 0) FirstOrderAt = when;
        OrderCount++;
        TotalSpent += amount;
        LastOrderAt = when;
    }
}
