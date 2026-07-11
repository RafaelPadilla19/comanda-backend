namespace Comanda.Domain.Enums;

/// <summary>Rol operativo del usuario dentro del restaurante.</summary>
public enum UserRole
{
    Administradora = 0,
    Cajero = 1,
    Mesera = 2,
    Cocina = 3,
}

/// <summary>Estado de un pedido dentro del tablero kanban.</summary>
public enum OrderStatus
{
    Nuevos = 0,
    Preparacion = 1,
    Listos = 2,
    Entregados = 3,
}

/// <summary>Tipo de movimiento de caja.</summary>
public enum MovementType
{
    Ingreso = 0,
    Egreso = 1,
    Fondo = 2,
}

/// <summary>Estado de la suscripción de un restaurante al SaaS.</summary>
public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2,   // suspensión dura del operador (abuso/fraude): no puede iniciar sesión
    Cancelled = 3,
    PastDue = 4,      // venció el plan pago tras el periodo de gracia: entra pero solo a /suscripcion
}

/// <summary>Tipo de descuento de un cupón.</summary>
public enum DiscountType
{
    /// <summary>Porcentaje sobre el subtotal (Value = 10 → 10%).</summary>
    Percentage = 0,
    /// <summary>Monto fijo en US$ (Value = 5 → $5).</summary>
    Fixed = 1,
}

/// <summary>Canal / tipo de pedido (de dónde llega y cómo se entrega).</summary>
public enum OrderChannel
{
    /// <summary>Consumo en el local (mesa).</summary>
    Local = 0,
    /// <summary>Para llevar (pickup en mostrador).</summary>
    Llevar = 1,
    /// <summary>Entrega a domicilio.</summary>
    Delivery = 2,
}
