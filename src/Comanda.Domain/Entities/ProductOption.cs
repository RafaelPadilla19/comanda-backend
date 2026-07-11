namespace Comanda.Domain.Entities;

/// <summary>
/// Opción de un producto (variante o extra) con su costo adicional sobre el precio base.
/// Variante: elección única (p.ej. tamaño). Extra: complemento sumable (p.ej. queso extra).
/// Una variante "gratis" simplemente tiene Price = 0.
/// </summary>
public class ProductOption
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
