using Comanda.Domain.Common;
using Comanda.Domain.Entities;

namespace Comanda.Domain.Abstractions;

/// <summary>Alta self-service de un restaurante: crea el tenant, su admin y datos semilla.</summary>
public interface ITenantProvisioningService
{
    Task<Result<User>> RegisterAsync(
        string restaurantName, string adminName, string email, string password, string country, CancellationToken ct = default);
}
