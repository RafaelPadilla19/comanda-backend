using Comanda.Api.Common;
using Comanda.Api.Contracts;
using Comanda.Api.Mapping;
using Comanda.Domain.Abstractions;
using Comanda.Domain.Common;
using Comanda.Domain.Entities;
using FastEndpoints;
using RMapper.Core.Interfaces;
using Order = Comanda.Domain.Entities.Order;

namespace Comanda.Api.Features.Customers;

// ============================================================
//  CRM: clientes derivados de los pedidos (clave = teléfono).
// ============================================================

public sealed class ListCustomersRequest
{
    /// <summary>Filtro opcional por nombre o teléfono.</summary>
    public string? Q { get; set; }
}

public sealed class ListCustomersEndpoint(IRepository<Customer> customers, IRMapper mapper)
    : Endpoint<ListCustomersRequest, List<CustomerDto>>
{
    public override void Configure() => Get("/customers");

    public override async Task HandleAsync(ListCustomersRequest req, CancellationToken ct)
    {
        var all = await customers.ListAsync(ct);
        IEnumerable<Customer> filtered = all;

        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var q = req.Q.Trim().ToLowerInvariant();
            var digits = new string(q.Where(char.IsDigit).ToArray());
            filtered = all.Where(c =>
                c.Name.ToLowerInvariant().Contains(q) ||
                (digits.Length > 0 && c.Phone.Contains(digits)));
        }

        var ordered = filtered.OrderByDescending(c => c.LastOrderAt);
        await Send.OkAsync(mapper.MapList<Customer, CustomerDto>(ordered), ct);
    }
}

public sealed class GetCustomerRequest { public Guid Id { get; set; } }

public sealed class GetCustomerEndpoint(IRepository<Customer> customers, IOrderRepository orders, IRMapper mapper)
    : Endpoint<GetCustomerRequest, CustomerDetailDto>
{
    public override void Configure() => Get("/customers/{id}");

    public override async Task HandleAsync(GetCustomerRequest req, CancellationToken ct)
    {
        if (await customers.GetByIdAsync(req.Id, ct) is not { } customer)
        {
            await HttpContext.SendErrorAsync(Error.NotFound("clientes.no_encontrado", "Cliente no encontrado."), ct);
            return;
        }

        var history = await orders.ListByCustomerAsync(customer.Id, ct);
        await Send.OkAsync(new CustomerDetailDto
        {
            Customer = mapper.Map<Customer, CustomerDto>(customer),
            Orders = mapper.MapList<Order, OrderDto>(history),
        }, ct);
    }
}
