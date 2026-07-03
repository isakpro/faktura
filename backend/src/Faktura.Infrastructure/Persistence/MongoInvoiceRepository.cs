using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;

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
}
