using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoInvoiceReminderRepository : TenantScopedRepository<InvoiceReminderDocument>, IInvoiceReminderRepository
{
    public MongoInvoiceReminderRepository(MongoContext context) : base(context.InvoiceReminders) { }

    public Task AddAsync(InvoiceReminder reminder, CancellationToken ct = default)
        => InsertAsync(InvoiceReminderDocument.FromDomain(reminder), ct);

    public async Task<IReadOnlyList<InvoiceReminder>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        var docs = await ListAsync(tenantId, ct);
        return docs.Where(d => d.InvoiceId == invoiceId)
            .OrderByDescending(d => d.SentAt)
            .Select(d => d.ToDomain())
            .ToList();
    }

    public async Task<bool> HasAutomaticAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        var count = await CountAsync(tenantId,
            Builders<InvoiceReminderDocument>.Filter.And(
                Builders<InvoiceReminderDocument>.Filter.Eq(d => d.InvoiceId, invoiceId),
                Builders<InvoiceReminderDocument>.Filter.Eq(d => d.Type, ReminderType.Automatic)),
            ct);
        return count > 0;
    }
}

internal sealed class MongoReminderSettingsRepository : IReminderSettingsRepository
{
    private readonly MongoContext _context;

    public MongoReminderSettingsRepository(MongoContext context) => _context = context;

    public async Task<ReminderSettings> GetAsync(string tenantId, CancellationToken ct = default)
    {
        var doc = await _context.ReminderSettings.Find(s => s.Id == tenantId).FirstOrDefaultAsync(ct);
        return doc?.ToDomain() ?? ReminderSettings.Default(tenantId);
    }

    public Task UpsertAsync(ReminderSettings settings, CancellationToken ct = default)
        => _context.ReminderSettings.ReplaceOneAsync(
            s => s.Id == settings.TenantId,
            ReminderSettingsDocument.FromDomain(settings),
            new ReplaceOptions { IsUpsert = true },
            ct);

    public async Task<IReadOnlyList<ReminderSettings>> ListAutoEnabledAsync(CancellationToken ct = default)
    {
        var docs = await _context.ReminderSettings.Find(s => s.AutoEnabled).ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }
}
