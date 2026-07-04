using Faktura.Domain.Common;

namespace Faktura.Domain.Invoicing;

/// <summary>Regler för när en betalningspåminnelse får skickas (FR-001/002).</summary>
public static class ReminderRules
{
    /// <summary>
    /// Endast riktiga fakturor (ej kreditfakturor) som är förfallna — skickade, obetalda och
    /// med passerat förfallodatum — får påminnas.
    /// </summary>
    public static Result CanRemind(Invoice invoice, DateOnly today)
    {
        if (invoice.Type != InvoiceType.Invoice) return Result.Failure(Error.InvalidState());
        return invoice.IsOverdue(today) ? Result.Success() : Result.Failure(Error.InvalidState());
    }

    /// <summary>Kvalificerar fakturan för det automatiska jobbet: förfallen i minst <paramref name="daysAfterDue"/> dagar.</summary>
    public static bool QualifiesForAutomatic(Invoice invoice, DateOnly today, int daysAfterDue)
        => invoice.Type == InvoiceType.Invoice
           && invoice.DueDate is { } due
           && invoice.IsOverdue(today)
           && due.AddDays(daysAfterDue) <= today;
}
