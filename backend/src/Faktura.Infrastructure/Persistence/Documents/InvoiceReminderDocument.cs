using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class InvoiceReminderDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string InvoiceId { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public ReminderType Type { get; set; }

    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public int Sequence { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ReminderStatus Status { get; set; }

    public string? Error { get; set; }
    public DateTime SentAt { get; set; }

    public static InvoiceReminderDocument FromDomain(InvoiceReminder r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        InvoiceId = r.InvoiceId,
        Type = r.Type,
        Recipient = r.Recipient,
        Subject = r.Subject,
        Sequence = r.Sequence,
        Status = r.Status,
        Error = r.Error,
        SentAt = r.SentAt
    };

    public InvoiceReminder ToDomain() =>
        new(Id, TenantId, InvoiceId, Type, Recipient, Subject, Sequence, Status, Error, SentAt);
}

internal sealed class ReminderSettingsDocument
{
    [BsonId] public string Id { get; set; } = ""; // = tenantId
    public bool AutoEnabled { get; set; }
    public int DaysAfterDue { get; set; } = ReminderSettings.DefaultDaysAfterDue;

    public static ReminderSettingsDocument FromDomain(ReminderSettings s) =>
        new() { Id = s.TenantId, AutoEnabled = s.AutoEnabled, DaysAfterDue = s.DaysAfterDue };

    public ReminderSettings ToDomain() => new(Id, AutoEnabled, DaysAfterDue);
}
