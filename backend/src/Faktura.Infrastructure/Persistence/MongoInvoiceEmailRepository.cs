using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoInvoiceEmailRepository : TenantScopedRepository<InvoiceEmailDocument>, IInvoiceEmailRepository
{
    public MongoInvoiceEmailRepository(MongoContext context) : base(context.InvoiceEmails) { }

    public Task AddAsync(InvoiceEmail email, CancellationToken ct = default)
        => InsertAsync(InvoiceEmailDocument.FromDomain(email), ct);

    public async Task<IReadOnlyList<InvoiceEmail>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        var docs = await ListAsync(tenantId, ct);
        return docs.Where(d => d.InvoiceId == invoiceId)
            .OrderByDescending(d => d.SentAt)
            .Select(d => d.ToDomain())
            .ToList();
    }
}
