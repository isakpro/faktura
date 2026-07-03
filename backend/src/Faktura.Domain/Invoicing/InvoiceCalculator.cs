using Faktura.Domain.Common;

namespace Faktura.Domain.Invoicing;

/// <summary>Momsbelopp för en enskild momssats.</summary>
public sealed record VatByRate(int RatePercent, Money Vat);

/// <summary>Fakturans summor: netto, moms per sats och brutto (att betala).</summary>
public sealed record InvoiceTotals(Money Net, IReadOnlyList<VatByRate> VatByRate, Money Gross);

/// <summary>
/// Beräknar fakturans summor från raderna. Netto och moms avrundas per rad (i <see cref="InvoiceLine"/>)
/// och summeras; brutto = summa netto + summa moms. Moms grupperas per sats (svenskt krav).
/// </summary>
public static class InvoiceCalculator
{
    public static InvoiceTotals Compute(IReadOnlyCollection<InvoiceLine> lines)
    {
        var net = Money.Zero;
        var totalVat = Money.Zero;
        foreach (var line in lines)
        {
            net += line.Net;
            totalVat += line.Vat;
        }

        var vatByRate = lines
            .GroupBy(l => (int)l.VatRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var sum = Money.Zero;
                foreach (var l in g) sum += l.Vat;
                return new VatByRate(g.Key, sum);
            })
            .ToList();

        var gross = net + totalVat;
        return new InvoiceTotals(net, vatByRate, gross);
    }
}
