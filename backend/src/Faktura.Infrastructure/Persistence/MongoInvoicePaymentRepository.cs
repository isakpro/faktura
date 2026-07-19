using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoInvoicePaymentRepository : TenantScopedRepository<InvoicePaymentDocument>, IInvoicePaymentRepository
{
    public MongoInvoicePaymentRepository(MongoContext context) : base(context.InvoicePayments) { }

    public Task AddAsync(InvoicePayment payment, CancellationToken ct = default)
        => InsertAsync(InvoicePaymentDocument.FromDomain(payment), ct);

    public async Task<IReadOnlyList<InvoicePayment>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        var docs = await ListAsync(tenantId, ct);
        return docs.Where(d => d.InvoiceId == invoiceId)
            .OrderByDescending(d => d.PaidDate).ThenByDescending(d => d.CreatedAt)
            .Select(d => d.ToDomain())
            .ToList();
    }
}
