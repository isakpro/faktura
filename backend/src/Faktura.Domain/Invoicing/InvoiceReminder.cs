namespace Faktura.Domain.Invoicing;

public enum ReminderType { Manual = 0, Automatic = 1 }
public enum ReminderStatus { Sent = 0, Failed = 1 }

/// <summary>Logg-post för en betalningspåminnelse. Lagras separat — fakturan förblir oföränderlig.</summary>
public sealed class InvoiceReminder
{
    public string Id { get; }
    public string TenantId { get; }
    public string InvoiceId { get; }
    public ReminderType Type { get; }
    public string Recipient { get; }
    public string Subject { get; }
    public int Sequence { get; }
    public ReminderStatus Status { get; }
    public string? Error { get; }
    public DateTime SentAt { get; }

    public InvoiceReminder(string id, string tenantId, string invoiceId, ReminderType type, string recipient,
        string subject, int sequence, ReminderStatus status, string? error, DateTime sentAt)
    {
        Id = id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Type = type;
        Recipient = recipient;
        Subject = subject;
        Sequence = sequence;
        Status = status;
        Error = error;
        SentAt = sentAt;
    }

    public static InvoiceReminder Sent(string id, string tenantId, string invoiceId, ReminderType type,
        string recipient, string subject, int sequence, DateTime now)
        => new(id, tenantId, invoiceId, type, recipient, subject, sequence, ReminderStatus.Sent, null, now);

    public static InvoiceReminder Failed(string id, string tenantId, string invoiceId, ReminderType type,
        string recipient, string subject, int sequence, string error, DateTime now)
        => new(id, tenantId, invoiceId, type, recipient, subject, sequence, ReminderStatus.Failed, error, now);
}
