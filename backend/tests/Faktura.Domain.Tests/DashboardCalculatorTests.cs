using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class DashboardCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 5);
    private static int _n;

    /// <summary>Skickad faktura på 1250 kr brutto (1000 + 25 % moms) med givet fakturadatum.</summary>
    private static Invoice Sent(DateOnly invoiceDate, int termsDays = 30)
    {
        var inv = Invoice.CreateDraft($"i-{++_n}", "t-1", "c-1",
            [new InvoiceLine("Rad", 1, 1000m, VatRate.TwentyFive)], Now);
        inv.Send(_n, invoiceDate, new CustomerSnapshot("Kund", null, null, null, null), termsDays, Now);
        return inv;
    }

    private static Invoice Paid(DateOnly paidDate)
    {
        var inv = Sent(paidDate.AddDays(-10));
        inv.MarkPaid(paidDate, Now);
        return inv;
    }

    [Fact]
    public void Computes_outstanding_overdue_and_paid_this_year()
    {
        var invoices = new List<Invoice>
        {
            Invoice.CreateDraft("d-1", "t-1", "c-1", [new InvoiceLine("X", 1, 999m, VatRate.Zero)], Now), // utkast: ignoreras
            Sent(new DateOnly(2026, 6, 20)),          // skickad, ej förfallen (förfaller 20 jul)
            Sent(new DateOnly(2026, 1, 1)),           // skickad, förfallen (förföll 31 jan)
            Paid(new DateOnly(2026, 3, 10)),          // betald i år
            Paid(new DateOnly(2025, 11, 5)),          // betald förra året — ej "i år"
        };

        var figures = DashboardCalculator.Compute(invoices, Today);

        Assert.Equal(2500m, figures.Outstanding);     // två skickade à 1250
        Assert.Equal(1250m, figures.Overdue);         // varav en förfallen
        Assert.Equal(1250m, figures.PaidThisYear);    // endast 2026-betalningen
    }

    [Fact]
    public void Credit_notes_are_excluded_from_figures()
    {
        var original = Sent(new DateOnly(2026, 1, 1));
        var credit = Invoice.CreateCreditNote("cn-1", original, 99, new DateOnly(2026, 2, 1), Now);

        var figures = DashboardCalculator.Compute([original, credit], Today);

        Assert.Equal(1250m, figures.Outstanding); // krediten (−1250, "Sent") räknas inte in
        Assert.Equal(1250m, figures.Overdue);
    }

    [Fact]
    public void Monthly_series_has_12_points_oldest_first_with_zeros()
    {
        var invoices = new List<Invoice>
        {
            Paid(new DateOnly(2026, 7, 1)),   // innevarande månad
            Paid(new DateOnly(2026, 7, 3)),   // samma månad — summeras
            Paid(new DateOnly(2026, 2, 15)),
            Paid(new DateOnly(2025, 7, 20)),  // 12 månader bak — utanför fönstret (aug 2025–jul 2026)
        };

        var series = DashboardCalculator.Compute(invoices, Today).MonthlyRevenue;

        Assert.Equal(12, series.Count);
        Assert.Equal((2025, 8), (series[0].Year, series[0].Month));   // äldst först
        Assert.Equal((2026, 7), (series[^1].Year, series[^1].Month)); // nyast sist
        Assert.Equal(2500m, series[^1].Gross);                        // två betalningar i juli
        Assert.Equal(1250m, series.Single(m => m.Month == 2 && m.Year == 2026).Gross);
        Assert.Equal(0m, series.Single(m => m.Month == 12 && m.Year == 2025).Gross); // tom månad = 0
        Assert.DoesNotContain(series, m => m.Year == 2025 && m.Month == 7);          // utanför fönstret
    }
}
