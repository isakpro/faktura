namespace Faktura.Domain.Invoicing;

public sealed record MonthlyRevenue(int Year, int Month, decimal Gross);

/// <summary>Nyckeltal för översikten. Belopp i SEK (brutto).</summary>
public sealed record DashboardFigures(
    decimal Outstanding,
    decimal Overdue,
    decimal PaidThisYear,
    IReadOnlyList<MonthlyRevenue> MonthlyRevenue);

/// <summary>
/// Beräknar översiktens nyckeltal från fakturaaggregat. Endast riktiga fakturor räknas —
/// kreditfakturor exkluderas ur summorna (spec 006). Återanvänder fakturans egna totals
/// så definitionerna aldrig divergerar från beräkningen i 002.
/// </summary>
public static class DashboardCalculator
{
    public static DashboardFigures Compute(IReadOnlyCollection<Invoice> invoices, DateOnly today)
    {
        var real = invoices.Where(i => i.Type == InvoiceType.Invoice).ToList();

        var outstanding = 0m;
        var overdue = 0m;
        var paidThisYear = 0m;

        foreach (var invoice in real)
        {
            if (invoice.Status == InvoiceStatus.Sent)
            {
                // Kvarvarande saldo, inte brutto — delbetalningar räknas av (spec 012).
                var remaining = invoice.RemainingAmount;
                outstanding += remaining;
                if (invoice.IsOverdue(today)) overdue += remaining;
            }
            else if (invoice.Status == InvoiceStatus.Paid && invoice.PaidDate?.Year == today.Year)
            {
                paidThisYear += invoice.Totals.Gross.Amount;
            }
        }

        return new DashboardFigures(outstanding, overdue, paidThisYear, ComputeMonthly(real, today));
    }

    /// <summary>Summa betalt brutto per månad, alltid exakt 12 punkter (äldst → nyast).</summary>
    private static List<MonthlyRevenue> ComputeMonthly(IEnumerable<Invoice> invoices, DateOnly today)
    {
        var paidByMonth = invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate is not null)
            .GroupBy(i => (i.PaidDate!.Value.Year, i.PaidDate.Value.Month))
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Totals.Gross.Amount));

        var series = new List<MonthlyRevenue>(12);
        for (var offset = 11; offset >= 0; offset--)
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-offset);
            paidByMonth.TryGetValue((month.Year, month.Month), out var gross);
            series.Add(new MonthlyRevenue(month.Year, month.Month, gross));
        }
        return series;
    }
}
