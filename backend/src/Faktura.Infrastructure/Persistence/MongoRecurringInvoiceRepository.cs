using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoRecurringInvoiceRepository : TenantScopedRepository<RecurringInvoiceDocument>, IRecurringInvoiceRepository
{
    public MongoRecurringInvoiceRepository(MongoContext context) : base(context.RecurringInvoices) { }

    public Task AddAsync(RecurringInvoice recurring, CancellationToken ct = default)
        => InsertAsync(RecurringInvoiceDocument.FromDomain(recurring), ct);

    public async Task<RecurringInvoice?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, id, ct))?.ToDomain();

    public async Task<IReadOnlyList<RecurringInvoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public Task UpdateAsync(RecurringInvoice recurring, CancellationToken ct = default)
        => ReplaceAsync(recurring.TenantId, recurring.Id, RecurringInvoiceDocument.FromDomain(recurring), ct);

    // Systemkontext (jobbet): läser över alla tenants — se IRecurringInvoiceRepository.
    public async Task<IReadOnlyList<RecurringInvoice>> ListDueAsync(DateOnly today, CancellationToken ct = default)
    {
        var cutoff = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var docs = await Collection
            .Find(d => d.Status == RecurringStatus.Active && d.NextRunDate <= cutoff)
            .ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }
}
