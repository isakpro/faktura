using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Abstractions;

public interface IInvoiceReminderRepository
{
    Task AddAsync(InvoiceReminder reminder, CancellationToken ct = default);

    /// <summary>Tenant-scoped historik för en faktura, senaste först.</summary>
    Task<IReadOnlyList<InvoiceReminder>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default);

    /// <summary>True om fakturan redan har en automatisk påminnelse-post (dubblettskydd, FR-008).</summary>
    Task<bool> HasAutomaticAsync(string tenantId, string invoiceId, CancellationToken ct = default);
}

public interface IReminderSettingsRepository
{
    /// <summary>Organisationens inställning; saknas den returneras standard (av, 7 dagar).</summary>
    Task<ReminderSettings> GetAsync(string tenantId, CancellationToken ct = default);

    Task UpsertAsync(ReminderSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Alla organisationer med automatiken påslagen. Systemkontext för det dagliga jobbet —
    /// medvetet undantag från per-request-tenantfiltret (endast läsning; skrivningar sker
    /// alltid med explicit tenantId).
    /// </summary>
    Task<IReadOnlyList<ReminderSettings>> ListAutoEnabledAsync(CancellationToken ct = default);
}
