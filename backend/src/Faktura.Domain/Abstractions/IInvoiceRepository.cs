using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetByIdAsync(string tenantId, string invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Systemkontext (spec 013): slår upp en faktura på portal-token, per definition utan
    /// tenant-filter — 128-bit-token ÄR behörigheten (kapabilitets-URL).
    /// </summary>
    Task<Invoice?> GetByShareTokenAsync(string shareToken, CancellationToken ct = default);
}

/// <summary>
/// Atomisk, löpande nummerserie per tenant. Garanterar unika, obrutna nummer även vid
/// samtidiga skick (FR-009).
/// </summary>
public interface IInvoiceNumberSequence
{
    Task<long> NextAsync(string tenantId, CancellationToken ct = default);
}
