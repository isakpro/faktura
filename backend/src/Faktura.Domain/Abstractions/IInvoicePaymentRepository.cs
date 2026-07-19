using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

public interface IInvoicePaymentRepository
{
    Task AddAsync(InvoicePayment payment, CancellationToken ct = default);
    Task<IReadOnlyList<InvoicePayment>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default);
}
