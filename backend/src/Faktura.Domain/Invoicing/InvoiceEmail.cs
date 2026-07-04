namespace Faktura.Domain.Invoicing;

public enum InvoiceEmailStatus { Sent = 0, Failed = 1 }

/// <summary>Logg-post för ett e-postutskick av en faktura. Lagras separat så fakturan förblir oföränderlig.</summary>
public sealed class InvoiceEmail
{
    public string Id { get; }
    public string TenantId { get; }
    public string InvoiceId { get; }
    public string Recipient { get; }
    public string Subject { get; }
    public InvoiceEmailStatus Status { get; }
    public string? Error { get; }
    public DateTime SentAt { get; }

    public InvoiceEmail(string id, string tenantId, string invoiceId, string recipient, string subject,
        InvoiceEmailStatus status, string? error, DateTime sentAt)
    {
        Id = id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Recipient = recipient;
        Subject = subject;
        Status = status;
        Error = error;
        SentAt = sentAt;
    }

    public static InvoiceEmail Sent(string id, string tenantId, string invoiceId, string recipient, string subject, DateTime now)
        => new(id, tenantId, invoiceId, recipient, subject, InvoiceEmailStatus.Sent, null, now);

    public static InvoiceEmail Failed(string id, string tenantId, string invoiceId, string recipient, string subject, string error, DateTime now)
        => new(id, tenantId, invoiceId, recipient, subject, InvoiceEmailStatus.Failed, error, now);
}
