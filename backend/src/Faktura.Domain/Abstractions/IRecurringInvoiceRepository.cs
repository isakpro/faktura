using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

public interface IRecurringInvoiceRepository
{
    Task AddAsync(RecurringInvoice recurring, CancellationToken ct = default);
    Task<RecurringInvoice?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringInvoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task UpdateAsync(RecurringInvoice recurring, CancellationToken ct = default);

    /// <summary>
    /// Alla aktiva mallar vars nästa körning passerats — över alla tenants. Systemkontext för
    /// det dagliga jobbet (endast läsning; skrivningar sker alltid med explicit tenantId).
    /// </summary>
    Task<IReadOnlyList<RecurringInvoice>> ListDueAsync(DateOnly today, CancellationToken ct = default);
}
