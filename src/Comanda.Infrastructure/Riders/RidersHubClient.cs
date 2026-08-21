using System.Net.Http.Json;
using Comanda.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Comanda.Infrastructure.Riders;

/// <summary>
/// Cliente HTTP de RidersHub. Envía la API key (X-Api-Key) e incluye un secreto de
/// callback (CallbackKey) que RidersHub reenvía firmado para que Comanda valide el aviso
/// cuando un rider acepta o entrega el pedido.
/// Config: Services:RidersHubUrl, Services:SelfBaseUrl; secretos: Services:RidersApiKey,
/// Riders:CallbackSecret.
/// </summary>
public sealed class RidersHubClient(HttpClient http, IConfiguration config, ILogger<RidersHubClient> logger)
    : IRidersClient
{
    public async Task<RiderJobResult?> PublishJobAsync(
        string tenantId, string orderId, string orderCode, string restaurantName,
        string pickupAddress, string dropoffAddress, string zone, decimal deliveryFee, string notes,
        string customerName, string customerPhone, double? pickupLat, double? pickupLng,
        CancellationToken ct = default)
    {
        var selfBase = config["Services:SelfBaseUrl"] ?? "http://localhost:5057";
        var body = new
        {
            tenantId, orderId, orderCode, restaurantName,
            pickupAddress, dropoffAddress, zone, deliveryFee, notes,
            customerName, customerPhone, pickupLat, pickupLng,
            callbackUrl = $"{selfBase}/api/public/riders/callback",
            callbackKey = config["Riders:CallbackSecret"] ?? string.Empty,
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/jobs") { Content = JsonContent.Create(body) };
            req.Headers.Add("X-Api-Key", config["Services:RidersApiKey"] ?? string.Empty);

            var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("RidersHub respondió {Status} al publicar el job del pedido {OrderId}", res.StatusCode, orderId);
                return null;
            }

            var dto = await res.Content.ReadFromJsonAsync<JobResponse>(ct);
            return dto is null ? null : new RiderJobResult(dto.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error llamando a RidersHub (pedido {OrderId})", orderId);
            return null;
        }
    }

    public async Task<RiderLocationResult?> GetJobLocationAsync(Guid jobId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"/internal/jobs/{jobId}/location");
            req.Headers.Add("X-Api-Key", config["Services:RidersApiKey"] ?? string.Empty);

            var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var dto = await res.Content.ReadFromJsonAsync<LocationResponse>(ct);
            return dto is null ? null : new RiderLocationResult(dto.JobStatus, dto.Lat, dto.Lng, dto.UpdatedAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error consultando ubicación del rider (job {JobId})", jobId);
            return null;
        }
    }

    private sealed record JobResponse(Guid Id);
    private sealed record LocationResponse(string JobStatus, double? Lat, double? Lng, DateTime? UpdatedAt);
}
