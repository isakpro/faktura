using Faktura.Domain.Customers;

namespace Faktura.Domain.Abstractions;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task<Customer?> GetByIdAsync(string tenantId, string customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
}
