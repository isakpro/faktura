using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

public interface IInvoiceEmailRepository
{
    Task AddAsync(InvoiceEmail email, CancellationToken ct = default);

    /// <summary>Tenant-scoped utskickshistorik för en faktura.</summary>
    Task<IReadOnlyList<InvoiceEmail>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default);
}
