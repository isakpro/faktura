using Faktura.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

/// <summary>
/// Holds the MongoDB collections and creates indexes. Index creation is explicit
/// (called at startup) rather than on every request.
/// </summary>
public sealed class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoOptions> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.Database);
    }

    internal IMongoCollection<OrganizationDocument> Organizations => _database.GetCollection<OrganizationDocument>("organizations");
    internal IMongoCollection<UserDocument> Users => _database.GetCollection<UserDocument>("users");
    internal IMongoCollection<RefreshTokenDocument> RefreshTokens => _database.GetCollection<RefreshTokenDocument>("refreshTokens");
    internal IMongoCollection<InvitationDocument> Invitations => _database.GetCollection<InvitationDocument>("invitations");
    internal IMongoCollection<ProcessedEventDocument> ProcessedEvents => _database.GetCollection<ProcessedEventDocument>("processedStripeEvents");
    internal IMongoCollection<CustomerDocument> Customers => _database.GetCollection<CustomerDocument>("customers");
    internal IMongoCollection<InvoiceDocument> Invoices => _database.GetCollection<InvoiceDocument>("invoices");
    internal IMongoCollection<InvoiceCounterDocument> InvoiceCounters => _database.GetCollection<InvoiceCounterDocument>("invoiceCounters");
    internal IMongoCollection<InvoiceEmailDocument> InvoiceEmails => _database.GetCollection<InvoiceEmailDocument>("invoiceEmails");
    internal IMongoCollection<InvoiceReminderDocument> InvoiceReminders => _database.GetCollection<InvoiceReminderDocument>("invoiceReminders");
    internal IMongoCollection<ReminderSettingsDocument> ReminderSettings => _database.GetCollection<ReminderSettingsDocument>("reminderSettings");
    internal IMongoCollection<ArticleDocument> Articles => _database.GetCollection<ArticleDocument>("articles");
    internal IMongoCollection<RecurringInvoiceDocument> RecurringInvoices => _database.GetCollection<RecurringInvoiceDocument>("recurringInvoices");
    internal IMongoCollection<AuditEntryDocument> AuditLog => _database.GetCollection<AuditEntryDocument>("auditLog");
    internal IMongoCollection<PasswordResetDocument> PasswordResets => _database.GetCollection<PasswordResetDocument>("passwordResets");
    internal IMongoCollection<InvoicePaymentDocument> InvoicePayments => _database.GetCollection<InvoicePaymentDocument>("invoicePayments");

    /// <summary>Pingar databasen — används av readiness-hälsokontrollen.</summary>
    public Task PingAsync(CancellationToken ct = default)
        => _database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: ct);

    /// <summary>Creates indexes described in data-model.md. Safe to call repeatedly.</summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Organizations.Indexes.CreateOneAsync(
            new CreateIndexModel<OrganizationDocument>(
                Builders<OrganizationDocument>.IndexKeys.Ascending(o => o.StripeCustomerId),
                new CreateIndexOptions { Name = "ix_org_stripe_customer", Sparse = true }), cancellationToken: ct);

        await Users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_user_email" }),
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(u => u.TenantId).Ascending(u => u.Role),
                new CreateIndexOptions { Name = "ix_user_tenant_role" })
        }, ct);

        await RefreshTokens.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshTokenDocument>(
                Builders<RefreshTokenDocument>.IndexKeys.Ascending(r => r.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_refresh_hash" }),
            new CreateIndexModel<RefreshTokenDocument>(
                Builders<RefreshTokenDocument>.IndexKeys.Ascending(r => r.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_refresh_expires", ExpireAfter = TimeSpan.Zero })
        }, ct);

        await Invitations.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InvitationDocument>(
                Builders<InvitationDocument>.IndexKeys.Ascending(i => i.TenantId).Ascending(i => i.Email),
                new CreateIndexOptions { Name = "ix_invitation_tenant_email" }),
            new CreateIndexModel<InvitationDocument>(
                Builders<InvitationDocument>.IndexKeys.Ascending(i => i.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_invitation_token" })
        }, ct);

        await Customers.Indexes.CreateOneAsync(
            new CreateIndexModel<CustomerDocument>(
                Builders<CustomerDocument>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.Name),
                new CreateIndexOptions { Name = "ix_customer_tenant_name" }), cancellationToken: ct);

        await Invoices.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InvoiceDocument>(
                Builders<InvoiceDocument>.IndexKeys.Ascending(i => i.TenantId).Ascending(i => i.Status),
                new CreateIndexOptions { Name = "ix_invoice_tenant_status" }),
            new CreateIndexModel<InvoiceDocument>(
                Builders<InvoiceDocument>.IndexKeys.Ascending(i => i.TenantId).Ascending(i => i.Number),
                new CreateIndexOptions { Name = "ux_invoice_tenant_number", Unique = true, Sparse = true }),
            // Portal-uppslag (013): globalt unik token; partial så dokument utan token inte indexeras.
            new CreateIndexModel<InvoiceDocument>(
                Builders<InvoiceDocument>.IndexKeys.Ascending(i => i.ShareToken),
                new CreateIndexOptions<InvoiceDocument>
                {
                    Name = "ux_invoice_sharetoken",
                    Unique = true,
                    PartialFilterExpression = Builders<InvoiceDocument>.Filter.Exists(i => i.ShareToken)
                })
        }, ct);

        await InvoiceEmails.Indexes.CreateOneAsync(
            new CreateIndexModel<InvoiceEmailDocument>(
                Builders<InvoiceEmailDocument>.IndexKeys.Ascending(e => e.TenantId).Ascending(e => e.InvoiceId),
                new CreateIndexOptions { Name = "ix_invoiceemail_tenant_invoice" }), cancellationToken: ct);

        await InvoicePayments.Indexes.CreateOneAsync(
            new CreateIndexModel<InvoicePaymentDocument>(
                Builders<InvoicePaymentDocument>.IndexKeys.Ascending(p => p.TenantId).Ascending(p => p.InvoiceId),
                new CreateIndexOptions { Name = "ix_payment_tenant_invoice" }), cancellationToken: ct);

        await InvoiceReminders.Indexes.CreateOneAsync(
            new CreateIndexModel<InvoiceReminderDocument>(
                Builders<InvoiceReminderDocument>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.InvoiceId),
                new CreateIndexOptions { Name = "ix_reminder_tenant_invoice" }), cancellationToken: ct);

        await PasswordResets.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PasswordResetDocument>(
                Builders<PasswordResetDocument>.IndexKeys.Ascending(p => p.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_pwreset_hash" }),
            new CreateIndexModel<PasswordResetDocument>(
                Builders<PasswordResetDocument>.IndexKeys.Ascending(p => p.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_pwreset_expires", ExpireAfter = TimeSpan.Zero })
        }, ct);

        await AuditLog.Indexes.CreateOneAsync(
            new CreateIndexModel<AuditEntryDocument>(
                Builders<AuditEntryDocument>.IndexKeys.Ascending(a => a.TenantId).Descending(a => a.OccurredAt),
                new CreateIndexOptions { Name = "ix_audit_tenant_time" }), cancellationToken: ct);

        await RecurringInvoices.Indexes.CreateOneAsync(
            new CreateIndexModel<RecurringInvoiceDocument>(
                Builders<RecurringInvoiceDocument>.IndexKeys.Ascending(r => r.Status).Ascending(r => r.NextRunDate),
                new CreateIndexOptions { Name = "ix_recurring_status_nextrun" }), cancellationToken: ct);

        await Articles.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ArticleDocument>(
                Builders<ArticleDocument>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.Name),
                new CreateIndexOptions { Name = "ix_article_tenant_name" }),
            // Partial (inte sparse!): i ett compound-index räcker det att tenantId finns för att
            // dokumentet ska indexeras — sparse skulle alltså ge kollision mellan artiklar UTAN sku.
            // Partial-filtret begränsar unikheten till dokument som faktiskt har ett sku.
            new CreateIndexModel<ArticleDocument>(
                Builders<ArticleDocument>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.Sku),
                new CreateIndexOptions<ArticleDocument>
                {
                    Name = "ux_article_tenant_sku",
                    Unique = true,
                    PartialFilterExpression = Builders<ArticleDocument>.Filter.Exists(a => a.Sku)
                })
        }, ct);
    }
}
