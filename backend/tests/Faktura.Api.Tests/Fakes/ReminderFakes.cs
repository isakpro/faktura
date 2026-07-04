using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Tests.Fakes;

/// <summary>Styrbar klocka: startar på verklig tid och kan flyttas framåt (gör fakturor förfallna).</summary>
public sealed class MutableClock : IClock
{
    private DateTime _now = DateTime.UtcNow;

    public DateTime UtcNow => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

public sealed class InMemoryInvoiceReminderRepository : IInvoiceReminderRepository
{
    private readonly List<InvoiceReminder> _items = new();

    public Task AddAsync(InvoiceReminder reminder, CancellationToken ct = default)
    {
        lock (_items) _items.Add(reminder);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InvoiceReminder>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult<IReadOnlyList<InvoiceReminder>>(
                _items.Where(r => r.TenantId == tenantId && r.InvoiceId == invoiceId)
                    .OrderByDescending(r => r.SentAt).ToList());
    }

    public Task<bool> HasAutomaticAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult(_items.Any(r =>
                r.TenantId == tenantId && r.InvoiceId == invoiceId && r.Type == ReminderType.Automatic));
    }
}

public sealed class InMemoryReminderSettingsRepository : IReminderSettingsRepository
{
    private readonly Dictionary<string, ReminderSettings> _items = new();

    public Task<ReminderSettings> GetAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult(_items.GetValueOrDefault(tenantId) ?? ReminderSettings.Default(tenantId));
    }

    public Task UpsertAsync(ReminderSettings settings, CancellationToken ct = default)
    {
        lock (_items) _items[settings.TenantId] = settings;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReminderSettings>> ListAutoEnabledAsync(CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult<IReadOnlyList<ReminderSettings>>(_items.Values.Where(s => s.AutoEnabled).ToList());
    }
}
