namespace Faktura.Domain.Invoicing;

/// <summary>
/// Reskontra-post: en registrerad betalning mot en faktura (spec 012).
/// Lagras separat så fakturan förblir oföränderlig; fakturans <c>PaidAmount</c> är summan.
/// </summary>
public sealed class InvoicePayment
{
    public string Id { get; }
    public string TenantId { get; }
    public string InvoiceId { get; }
    public decimal Amount { get; }
    public DateOnly PaidDate { get; }
    public string? Note { get; }
    public DateTime CreatedAt { get; }

    public InvoicePayment(string id, string tenantId, string invoiceId, decimal amount, DateOnly paidDate,
        string? note, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Amount = amount;
        PaidDate = paidDate;
        Note = note;
        CreatedAt = createdAt;
    }
}
