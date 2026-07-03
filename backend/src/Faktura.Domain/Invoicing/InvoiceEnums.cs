namespace Faktura.Domain.Invoicing;

/// <summary>Lagrad fakturastatus. "Förfallen" härleds (ej lagrad) från dueDate + obetald.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Sent = 1,
    Paid = 2,
    Credited = 3
}

public enum InvoiceType
{
    Invoice = 0,
    CreditNote = 1
}
