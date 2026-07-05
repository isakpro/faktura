using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Tests.Fakes;

public sealed class InMemoryRecurringInvoiceRepository : IRecurringInvoiceRepository
{
    private readonly ConcurrentDictionary<string, RecurringInvoice> _items = new();

    public Task AddAsync(RecurringInvoice recurring, CancellationToken ct = default)
    {
        _items[recurring.Id] = recurring;
        return Task.CompletedTask;
    }

    public Task<RecurringInvoice?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(r => r.Id == id && r.TenantId == tenantId));

    public Task<IReadOnlyList<RecurringInvoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecurringInvoice>>(_items.Values.Where(r => r.TenantId == tenantId).ToList());

    public Task UpdateAsync(RecurringInvoice recurring, CancellationToken ct = default)
    {
        _items[recurring.Id] = recurring;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecurringInvoice>> ListDueAsync(DateOnly today, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecurringInvoice>>(
            _items.Values.Where(r => r.IsDue(today)).ToList());
}
