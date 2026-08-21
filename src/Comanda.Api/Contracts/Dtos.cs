using Comanda.Domain.Enums;

namespace Comanda.Api.Contracts;

// DTOs de lectura con propiedades mutables y constructor vacío
// (requisito de RMapper, que crea la instancia destino y asigna propiedades).
// Los enums se serializan como texto (JsonStringEnumConverter).

/// <summary>Ubicación en vivo del rider de un pedido, para el mapa del cliente en la tienda pública.</summary>
public sealed class OrderRiderLocationDto
{
    public bool Available { get; set; }        // false si el pedido no tiene rider externo asignado
    public string JobStatus { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    /// <summary>Funciones incluidas en el plan del restaurante (para ocultar/gatear la UI). Null = sin plan (permisivo).</summary>
    public PlanFeaturesDto? Plan { get; set; }
    /// <summary>Estado de la suscripción (para bloquear la app si está vencida/PastDue).</summary>
    public SubscriptionStatus SubscriptionStatus { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    /// <summary>Fecha en que vence el trial de campaña (Premium gratis), si aplica.</summary>
    public DateTime? TrialEndsAt { get; set; }
}

/// <summary>Funciones del plan que la UI usa para mostrar/ocultar secciones.</summary>
public sealed class PlanFeaturesDto
{
    public string PlanName { get; set; } = string.Empty;
    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }
}

public sealed class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    public string WhatsappPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? CoverageRadiusKm { get; set; }
    public decimal DeliveryBaseFee { get; set; }
    public decimal DeliveryFeePerKm { get; set; }
}

public sealed class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class ProductOptionDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Tint { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public List<ProductOptionDto> Variants { get; set; } = new();
    public List<ProductOptionDto> Extras { get; set; } = new();
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public sealed class OrderItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Modifiers { get; set; } = string.Empty;  // "Maíz · +Queso extra"
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public sealed class OrderDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public OrderChannel Channel { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal DeliveryFee { get; set; }
    public string DeliveryZoneName { get; set; } = string.Empty;
    public double? DeliveryDistanceKm { get; set; }
    public double? CustomerLat { get; set; }
    public double? CustomerLng { get; set; }
    public Guid? DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public DateTime? DispatchedAt { get; set; }
    public Guid? RiderJobId { get; set; }
    public string RiderJobStatus { get; set; } = string.Empty;
    public decimal? RiderProposedFee { get; set; }
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public decimal TipRestaurant { get; set; }
    public decimal TipRider { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }
    public bool IsPaid { get; set; }
    /// <summary>Link de pago (solo se llena al crear un pedido con pago en línea).</summary>
    public string PaymentUrl { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public sealed class DeliveryZoneDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public bool IsActive { get; set; }
}

public sealed class DriverDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AvgTicket { get; set; }
    public int Points { get; set; }
    public DateTime FirstOrderAt { get; set; }
    public DateTime LastOrderAt { get; set; }
}

public sealed class LoyaltyConfigDto
{
    public bool Enabled { get; set; }
    public decimal EarnRate { get; set; }     // puntos por US$1
    public int RedeemRate { get; set; }       // puntos por US$1 de descuento
}

/// <summary>Saldo de puntos de un cliente para el checkout público.</summary>
public sealed class LoyaltyLookupDto
{
    public bool Enabled { get; set; }
    public int Points { get; set; }
    public int RedeemRate { get; set; }
    public decimal RedeemableAmount { get; set; }  // US$ que puede canjear ahora
    /// <summary>Nombre del cliente si ya hizo un pedido antes; vacío si es la primera vez.</summary>
    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Token secreto para guardar en este dispositivo. Solo viene la primera vez que se
    /// reclama (o si ya lo tenías); si otro dispositivo consulta el mismo teléfono, viene vacío
    /// para no filtrar el token real.</summary>
    public string Token { get; set; } = string.Empty;
}

public sealed class CustomerDetailDto
{
    public CustomerDto Customer { get; set; } = new();
    public List<OrderDto> Orders { get; set; } = new();
}

public sealed class CouponDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Resultado de validar un cupón en el checkout (tienda/POS).</summary>
public sealed class CouponValidationDto
{
    public bool Valid { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Discount { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ---- Control-Plane / Super-Admin ----

public sealed class PlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public int MaxBranches { get; set; }
    public int MaxProducts { get; set; }
    public int MaxUsers { get; set; }
    public int MaxOrdersMonth { get; set; }
    public decimal OveragePrice { get; set; }
    public int RetentionMonths { get; set; }
    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Consumo del tenant frente a los límites de su plan (pantalla de suscripción).</summary>
public sealed class PlanUsageDto
{
    public bool HasPlan { get; set; }
    public string PlanName { get; set; } = "Sin plan";
    public int OrdersThisMonth { get; set; }
    public int MaxOrdersMonth { get; set; }
    public decimal OveragePrice { get; set; }
    public int OverageOrders { get; set; }
    public decimal OverageAmount { get; set; }
    public bool OverageAvailable { get; set; }
    public bool AllowOverage { get; set; }
    public int Branches { get; set; }
    public int MaxBranches { get; set; }
    public int Products { get; set; }
    public int MaxProducts { get; set; }
    public int Users { get; set; }
    public int MaxUsers { get; set; }
    public int RetentionMonths { get; set; }
    public bool OnlinePayments { get; set; }
    public bool Coupons { get; set; }
    public bool Loyalty { get; set; }
    public bool AdvancedReports { get; set; }
    public bool Inventory { get; set; }
}

public sealed class PlatformAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class PlatformLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public PlatformAdminDto Admin { get; set; } = new();
}

/// <summary>Tenant con su plan, estado y métricas, para la consola del operador.</summary>
public sealed class PlatformTenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; }
    public Guid? PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public int Branches { get; set; }
    public int Products { get; set; }
    public int Users { get; set; }
    public int Orders { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---- Facturación de la suscripción (restaurante → Innovacors) ----

public sealed class SubscriptionPaymentDto
{
    public decimal Amount { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Hasta cuándo queda cubierta la suscripción con este pago (null si aún no se confirma).</summary>
    public DateTime? PeriodEndsAt { get; set; }
    public int PeriodMonths { get; set; }
}

public sealed class BillingDto
{
    public bool HasPlan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    /// <summary>Fecha en que vence el trial de campaña (Premium gratis), si aplica.</summary>
    public DateTime? TrialEndsAt { get; set; }
    public List<SubscriptionPaymentDto> Payments { get; set; } = new();
    public PlanUsageDto? Usage { get; set; }
}

public sealed class SubscriptionCheckoutResponse
{
    public bool Paid { get; set; }              // true si el plan es gratis (activado sin cobro)
    public string PaymentUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Estado de la conexión de pagos del restaurante (NO devuelve el secret).</summary>
public sealed class WompiConfigDto
{
    public string AppId { get; set; } = string.Empty;
    public bool Connected { get; set; }
}

public sealed class PlatformMetricsDto
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int TrialTenants { get; set; }
    public int SuspendedTenants { get; set; }
    public decimal Mrr { get; set; }           // ingreso recurrente mensual (planes activos)
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
}

// ---- Tienda pública (QR) ----

/// <summary>Producto tal cual lo ve el cliente en la tienda pública.</summary>
public sealed class PublicProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Tint { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ProductOptionDto> Variants { get; set; } = new();
    public List<ProductOptionDto> Extras { get; set; } = new();
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

/// <summary>Menú público de una sucursal: datos de la sucursal + categorías + productos disponibles.</summary>
public sealed class PublicMenuDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchAddress { get; set; } = string.Empty;
    public string BranchHours { get; set; } = string.Empty;
    public string BranchWhatsapp { get; set; } = string.Empty;
    public List<CategoryDto> Categories { get; set; } = new();
    public List<PublicProductDto> Products { get; set; } = new();
    public List<DeliveryZoneDto> DeliveryZones { get; set; } = new();

    /// <summary>Si la sucursal tiene radio de cobertura configurado, el checkout pide ubicación exacta en vez de zona.</summary>
    public double? BranchLat { get; set; }
    public double? BranchLng { get; set; }
    public double? CoverageRadiusKm { get; set; }
    public decimal DeliveryBaseFee { get; set; }
    public decimal DeliveryFeePerKm { get; set; }
}

public sealed class CashMovementDto
{
    public string Label { get; set; } = string.Empty;
    public string Sub { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public MovementType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CashSessionDto
{
    public Guid Id { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal SalesEfectivo { get; set; }
    public decimal SalesTarjeta { get; set; }
    public decimal SalesTransfer365 { get; set; }
    public decimal SalesTransfer { get; set; }
    public List<CashMovementDto> Movements { get; set; } = new();
}

public sealed class InventoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Min { get; set; }
    public decimal Cost { get; set; }
}

public sealed class PaymentMethodDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>País del restaurante (lectura/edición en Configuración).</summary>
public sealed class CountryDto
{
    public string Country { get; set; } = "SV";
}

/// <summary>Método de pago del catálogo por país (administrado en /platform).</summary>
public sealed class CountryPaymentMethodDto
{
    public Guid Id { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool DefaultEnabled { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PrinterDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Use { get; set; } = string.Empty;
    public string Connection { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}
