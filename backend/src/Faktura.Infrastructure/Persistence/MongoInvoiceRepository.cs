using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoInvoiceRepository : TenantScopedRepository<InvoiceDocument>, IInvoiceRepository
{
    public MongoInvoiceRepository(MongoContext context) : base(context.Invoices) { }

    public Task AddAsync(Invoice invoice, CancellationToken ct = default)
        => InsertAsync(InvoiceDocument.FromDomain(invoice), ct);

    public async Task<Invoice?> GetByIdAsync(string tenantId, string invoiceId, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, invoiceId, ct))?.ToDomain();

    public async Task<IReadOnlyList<Invoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
        => ReplaceAsync(invoice.TenantId, invoice.Id, InvoiceDocument.FromDomain(invoice), ct);

    public async Task<Invoice?> GetByShareTokenAsync(string shareToken, CancellationToken ct = default)
    {
        // Systemkontext (spec 013): medvetet utanför tenant-filtret — 128-bit-token är kapabiliteten.
        var filter = Builders<InvoiceDocument>.Filter.Eq(d => d.ShareToken, shareToken);
        var doc = await Collection.Find(filter).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }
}
