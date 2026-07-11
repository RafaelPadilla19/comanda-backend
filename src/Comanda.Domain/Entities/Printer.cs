using Comanda.Domain.Common;

namespace Comanda.Domain.Entities;

/// <summary>Impresora térmica configurada (cocina, caja, barra).</summary>
public class Printer : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;       // pr-cocina, pr-caja, pr-barra
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Use { get; set; } = string.Empty;
    public string Connection { get; set; } = string.Empty; // USB, Red (LAN), Bluetooth
    public bool IsConnected { get; set; }
}
