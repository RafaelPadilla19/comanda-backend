using System.Net.Http.Json;
using Comanda.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Comanda.Infrastructure.Payments;

/// <summary>
/// Cliente HTTP de PaymentsHub. Envía la API key (X-Api-Key) e incluye un secreto de
/// callback (CallbackKey) que PaymentsHub reenvía firmado para que Comanda valide el aviso.
/// Config: Services:PaymentsHubUrl, Services:SelfBaseUrl; secretos: Services:PaymentsApiKey,
/// Payments:CallbackSecret.
/// </summary>
public sealed class PaymentsHubClient(HttpClient http, IConfiguration config, ILogger<PaymentsHubClient> logger)
    : IPaymentsClient
{
    public Task<PaymentChargeResult?> CreateOrderChargeAsync(
        decimal amount, string currency, string description, string orderRef, string? tenantId,
        string returnUrl, string? wompiAppId, string? wompiApiSecret, CancellationToken ct = default)
        => CreateChargeAsync("Order", amount, currency, description, orderRef, tenantId, returnUrl, wompiAppId, wompiApiSecret, ct);

    public Task<PaymentChargeResult?> CreateSubscriptionChargeAsync(
        decimal amount, string description, string subscriptionRef, string tenantId,
        string returnUrl, string? wompiAppId, string? wompiApiSecret, CancellationToken ct = default)
        // Suscripción: usa las llaves Wompi de plataforma (Innovacors) configuradas en /platform.
        // Si van vacías, PaymentsHub usa su fallback de config.
        => CreateChargeAsync("Subscription", amount, "USD", description, subscriptionRef, tenantId, returnUrl, wompiAppId, wompiApiSecret, ct);

    private async Task<PaymentChargeResult?> CreateChargeAsync(
        string kind, decimal amount, string currency, string description, string orderRef, string? tenantId,
        string returnUrl, string? wompiAppId, string? wompiApiSecret, CancellationToken ct)
    {
        var selfBase = config["Services:SelfBaseUrl"] ?? "http://localhost:5057";
        var body = new
        {
            gateway = config["Services:PaymentsGateway"] ?? "fake",  // "wompi" en prod; "fake" para pruebas
            kind,
            amount,
            currency,
            description,
            tenantId,
            orderRef,
            returnUrl,
            callbackUrl = $"{selfBase}/api/public/payments/callback",
            callbackKey = config["Payments:CallbackSecret"] ?? string.Empty,
            // Llaves del restaurante (solo pedidos). Si van vacías, PaymentsHub usa su fallback (Innovacors).
            wompiAppId,
            wompiApiSecret,
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/charges") { Content = JsonContent.Create(body) };
            req.Headers.Add("X-Api-Key", config["Services:PaymentsApiKey"] ?? string.Empty);

            var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("PaymentsHub respondió {Status} al crear el cobro {Kind}/{Ref}", res.StatusCode, kind, orderRef);
                return null;
            }

            var dto = await res.Content.ReadFromJsonAsync<ChargeResponse>(ct);
            return dto is null ? null : new PaymentChargeResult(dto.Id, dto.PayUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error llamando a PaymentsHub ({Kind}/{Ref})", kind, orderRef);
            return null;
        }
    }

    private sealed record ChargeResponse(Guid Id, string PayUrl);
}
