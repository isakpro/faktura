using Faktura.Domain.Abstractions;
using Faktura.Domain.Customers;
using Faktura.Infrastructure.Persistence.Documents;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoCustomerRepository : TenantScopedRepository<CustomerDocument>, ICustomerRepository
{
    public MongoCustomerRepository(MongoContext context) : base(context.Customers) { }

    public Task AddAsync(Customer customer, CancellationToken ct = default)
        => InsertAsync(CustomerDocument.FromDomain(customer), ct);

    public async Task<Customer?> GetByIdAsync(string tenantId, string customerId, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, customerId, ct))?.ToDomain();

    public async Task<IReadOnlyList<Customer>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public Task UpdateAsync(Customer customer, CancellationToken ct = default)
        => ReplaceAsync(customer.TenantId, customer.Id, CustomerDocument.FromDomain(customer), ct);
}
