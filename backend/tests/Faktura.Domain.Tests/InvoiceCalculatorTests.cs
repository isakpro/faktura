using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class InvoiceCalculatorTests
{
    private static InvoiceLine Line(decimal qty, decimal price, VatRate rate) =>
        new("Rad", qty, price, rate);

    [Fact]
    public void Single_line_25_percent()
    {
        var totals = InvoiceCalculator.Compute([Line(10, 1200m, VatRate.TwentyFive)]);

        Assert.Equal(12000m, totals.Net.Amount);       // 10 × 1200
        Assert.Equal(3000m, totals.Gross.Amount - totals.Net.Amount); // moms 25 %
        Assert.Equal(15000m, totals.Gross.Amount);
        Assert.Single(totals.VatByRate);
        Assert.Equal(25, totals.VatByRate[0].RatePercent);
        Assert.Equal(3000m, totals.VatByRate[0].Vat.Amount);
    }

    [Fact]
    public void Mixed_rates_group_vat_per_rate()
    {
        var totals = InvoiceCalculator.Compute([
            Line(1, 1000m, VatRate.TwentyFive), // net 1000, vat 250
            Line(2, 500m, VatRate.Twelve),      // net 1000, vat 120
            Line(1, 300m, VatRate.Zero),        // net 300, vat 0
        ]);

        Assert.Equal(2300m, totals.Net.Amount);
        Assert.Equal(2670m, totals.Gross.Amount); // 2300 + 250 + 120
        // Grupperat per sats, fallande.
        Assert.Equal([25, 12, 0], totals.VatByRate.Select(v => v.RatePercent));
        Assert.Equal(250m, totals.VatByRate.First(v => v.RatePercent == 25).Vat.Amount);
        Assert.Equal(120m, totals.VatByRate.First(v => v.RatePercent == 12).Vat.Amount);
    }

    [Fact]
    public void Rounds_to_ore_and_subtotals_sum_to_total()
    {
        // 3 × 33.33 = 99.99 net; moms 25 % = 24.9975 -> 25.00 (away-from-zero)
        var totals = InvoiceCalculator.Compute([Line(3, 33.33m, VatRate.TwentyFive)]);

        Assert.Equal(99.99m, totals.Net.Amount);
        Assert.Equal(25.00m, totals.VatByRate[0].Vat.Amount);
        // Deltotaler summerar exakt till brutto (ingen öresdifferens).
        Assert.Equal(totals.Net.Amount + totals.VatByRate.Sum(v => v.Vat.Amount), totals.Gross.Amount);
    }

    [Fact]
    public void Empty_invoice_is_zero()
    {
        var totals = InvoiceCalculator.Compute([]);
        Assert.Equal(0m, totals.Net.Amount);
        Assert.Equal(0m, totals.Gross.Amount);
        Assert.Empty(totals.VatByRate);
    }
}
