using Comanda.Api.Contracts;
using Comanda.Domain.Entities;
using RMapper.Configuration;

namespace Comanda.Api.Mapping;

/// <summary>Perfil de RMapper: entidades de dominio → DTOs de la API.</summary>
public sealed class ComandaMappingProfile : Profile
{
    public ComandaMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : "Todas las sucursales"))
            .ForMember(d => d.TenantName, o => o.MapFrom(s => s.Tenant != null ? s.Tenant.Name : string.Empty));

        CreateMap<Branch, BranchDto>();
        CreateMap<Category, CategoryDto>();

        CreateMap<ProductOption, ProductOptionDto>();

        CreateMap<Product, ProductDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty));

        CreateMap<Product, PublicProductDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : string.Empty));

        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Order, OrderDto>();

        CreateMap<CashMovement, CashMovementDto>();
        CreateMap<CashSession, CashSessionDto>();

        CreateMap<InventoryItem, InventoryItemDto>();
        CreateMap<PaymentMethodConfig, PaymentMethodDto>();
        CreateMap<Printer, PrinterDto>();
        CreateMap<DeliveryZone, DeliveryZoneDto>();
        CreateMap<Driver, DriverDto>();

        CreateMap<Customer, CustomerDto>()
            .ForMember(d => d.AvgTicket, o => o.MapFrom(s => s.OrderCount > 0 ? s.TotalSpent / s.OrderCount : 0));

        CreateMap<Coupon, CouponDto>();
        CreateMap<Plan, PlanDto>();
    }
}
