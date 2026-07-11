namespace Comanda.Domain.Abstractions;

/// <summary>Resultado de iniciar un cobro: el id del cobro y el link de pago.</summary>
public sealed record PaymentChargeResult(Guid ChargeId, string PayUrl);

/// <summary>
/// Cliente del microservicio de pagos (PaymentsHub). Comanda lo consume por este puerto;
/// NO conoce el PSP concreto. La implementación vive en Infrastructure (HTTP + API key).
/// </summary>
public interface IPaymentsClient
{
    Task<PaymentChargeResult?> CreateOrderChargeAsync(
        decimal amount, string currency, string description, string orderRef, string? tenantId,
        string returnUrl, string? wompiAppId, string? wompiApiSecret, CancellationToken ct = default);

    /// <summary>
    /// Cobro de suscripción (restaurante → Innovacors). Las llaves Wompi de plataforma se
    /// configuran en la consola del operador; si van vacías, PaymentsHub usa su fallback.
    /// </summary>
    Task<PaymentChargeResult?> CreateSubscriptionChargeAsync(
        decimal amount, string description, string subscriptionRef, string tenantId,
        string returnUrl, string? wompiAppId, string? wompiApiSecret, CancellationToken ct = default);
}
