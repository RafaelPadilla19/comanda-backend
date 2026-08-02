namespace Comanda.Domain.Common;

/// <summary>Parámetros del ciclo de suscripción (Opción A: sin tarjeta guardada).</summary>
public static class SubscriptionPolicy
{
    /// <summary>Días después del vencimiento antes de marcar el plan como vencido (PastDue).</summary>
    public const int GraceDays = 3;

    /// <summary>Ventana para avisar "tu plan vence pronto".</summary>
    public const int ReminderDays = 3;

    /// <summary>Meses de acceso Premium gratis para restaurantes nuevos (campaña mientras se consiguen los primeros clientes).</summary>
    public const int TrialMonths = 6;
}
