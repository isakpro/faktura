using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Customers;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Tests.Fakes;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<string, Customer> _items = new();

    public Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        _items[customer.Id] = customer;
        return Task.CompletedTask;
    }

    public Task<Customer?> GetByIdAsync(string tenantId, string customerId, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(c => c.Id == customerId && c.TenantId == tenantId));

    public Task<IReadOnlyList<Customer>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Customer>>(_items.Values.Where(c => c.TenantId == tenantId).ToList());

    public Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _items[customer.Id] = customer;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<string, Invoice> _items = new();

    public Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        _items[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetByIdAsync(string tenantId, string invoiceId, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(i => i.Id == invoiceId && i.TenantId == tenantId));

    public Task<IReadOnlyList<Invoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_items.Values.Where(i => i.TenantId == tenantId).ToList());

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        _items[invoice.Id] = invoice;
        return Task.CompletedTask;
    }
}

/// <summary>Atomisk nummerserie i minne (AddOrUpdate är trådsäker per nyckel).</summary>
public sealed class InMemoryInvoiceNumberSequence : IInvoiceNumberSequence
{
    private readonly ConcurrentDictionary<string, long> _seq = new();

    public Task<long> NextAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_seq.AddOrUpdate(tenantId, 1, (_, v) => v + 1));
}
