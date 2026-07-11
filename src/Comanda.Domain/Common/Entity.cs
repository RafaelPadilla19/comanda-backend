namespace Comanda.Domain.Common;

/// <summary>Base para todas las entidades: identidad Guid y fecha de creación (UTC).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
