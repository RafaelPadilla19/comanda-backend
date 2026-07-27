namespace Comanda.Domain.Abstractions;

/// <summary>Resultado de publicar un pedido al pool de riders: el id del job en RidersHub.</summary>
public sealed record RiderJobResult(Guid JobId);

/// <summary>
/// Cliente del microservicio de riders independientes (RidersHub). Comanda lo consume por
/// este puerto cuando el restaurante no tiene un repartidor propio disponible.
/// La implementación vive en Infrastructure (HTTP + API key).
/// </summary>
public interface IRidersClient
{
    Task<RiderJobResult?> PublishJobAsync(
        string tenantId, string orderId, string orderCode, string restaurantName,
        string pickupAddress, string dropoffAddress, string zone, decimal deliveryFee, string notes,
        string customerName, string customerPhone, double? pickupLat, double? pickupLng,
        CancellationToken ct = default);
}
