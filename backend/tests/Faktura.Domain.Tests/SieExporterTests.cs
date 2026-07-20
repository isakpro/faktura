using Faktura.Domain.Accounting;
using Faktura.Domain.Customers;
using Faktura.Domain.Invoicing;
using Faktura.Domain.Organizations;
using Xunit;

namespace Faktura.Domain.Tests;

public class SieExporterTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);

    private static Organization Seller()
    {
        var org = Organization.CreateNew("o-1", "Ramstedt Konsult AB", freeSeatLimit: 2, Now);
        org.UpdateProfile(new InvoiceProfile("556677-8899", null, null, null, null, null, false));
        return org;
    }

    private static Invoice SentInvoice(string id, long number, DateOnly invoiceDate, params InvoiceLine[] lines)
    {
        var inv = Invoice.CreateDraft(id, "t-1", "c-1", lines, Now);
        inv.Send(number, invoiceDate, new CustomerSnapshot("Kund AB", null, null, null, null), 30, Now);
        return inv;
    }

    private static IReadOnlyList<(string Account, decimal Amount)> ParseTrans(string sie, string verNumber)
    {
        var lines = sie.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith($"#VER \"F\" \"{verNumber}\""));
        Assert.True(start >= 0, $"Verifikation {verNumber} hittades inte");
        var result = new List<(string, decimal)>();
        for (var i = start + 1; i < lines.Length && lines[i] != "}"; i++)
        {
            if (!lines[i].StartsWith("#TRANS ")) continue;
            var parts = lines[i].Split(' ');
            result.Add((parts[1], decimal.Parse(parts[^1], System.Globalization.CultureInfo.InvariantCulture)));
        }
        return result;
    }

    [Fact]
    public void Header_contains_sietyp_4_and_period()
    {
        var sie = SieExporter.Generate([], Seller(), 2026, Now);

        Assert.Contains("#SIETYP 4", sie);
        Assert.Contains("#RAR 0 20260101 20261231", sie);
        Assert.Contains("#ORGNR 556677-8899", sie);
        Assert.Contains("#FNAMN \"Ramstedt Konsult AB\"", sie);
    }

    [Fact]
    public void Verification_balances_across_multiple_vat_rates()
    {
        var invoice = SentInvoice("i-1", 1, new DateOnly(2026, 3, 10),
            new InvoiceLine("Konsult", 10, 1000m, VatRate.TwentyFive, "tim"),
            new InvoiceLine("Bok", 2, 500m, VatRate.Six));

        var sie = SieExporter.Generate([invoice], Seller(), 2026, Now);
        var trans = ParseTrans(sie, "1");

        Assert.Equal(0m, trans.Sum(t => t.Amount));
        Assert.Contains(trans, t => t.Account == "1510" && t.Amount == 13560m);   // AR debet
        Assert.Contains(trans, t => t.Account == "3001" && t.Amount == -10000m); // försäljning 25%
        Assert.Contains(trans, t => t.Account == "2611" && t.Amount == -2500m);  // moms 25%
        Assert.Contains(trans, t => t.Account == "3003" && t.Amount == -1000m); // försäljning 6%
        Assert.Contains(trans, t => t.Account == "2631" && t.Amount == -60m);   // moms 6%
    }

    [Fact]
    public void Zero_vat_line_has_no_vat_account()
    {
        var invoice = SentInvoice("i-2", 2, new DateOnly(2026, 3, 10), new InvoiceLine("Momsfritt", 1, 100m, VatRate.Zero));

        var sie = SieExporter.Generate([invoice], Seller(), 2026, Now);
        var trans = ParseTrans(sie, "2");

        Assert.Equal(2, trans.Count); // bara AR + försäljning, inget momskonto
        Assert.Contains(trans, t => t.Account == "3004" && t.Amount == -100m);
        Assert.DoesNotContain(trans, t => t.Account is "2611" or "2621" or "2631");
    }

    [Fact]
    public void Credit_note_reverses_the_entries()
    {
        var original = SentInvoice("i-3", 3, new DateOnly(2026, 5, 1), new InvoiceLine("Konsult", 1, 1000m, VatRate.TwentyFive));
        var creditLines = original.BuildCreditLines(null).Value;
        var credit = Invoice.CreateCreditNote("cn-1", original, 4, new DateOnly(2026, 5, 2), Now, creditLines);

        var sie = SieExporter.Generate([original, credit], Seller(), 2026, Now);
        var trans = ParseTrans(sie, "4");

        Assert.Equal(0m, trans.Sum(t => t.Amount));
        Assert.Contains(trans, t => t.Account == "1510" && t.Amount == -1250m); // kredit AR
        Assert.Contains(trans, t => t.Account == "3001" && t.Amount == 1000m);  // debet försäljning
        Assert.Contains(trans, t => t.Account == "2611" && t.Amount == 250m);   // debet moms
    }

    [Fact]
    public void Only_invoices_from_requested_year_are_included_and_drafts_excluded()
    {
        var inYear = SentInvoice("i-4", 5, new DateOnly(2026, 1, 1), new InvoiceLine("A", 1, 100m, VatRate.Zero));
        var otherYear = SentInvoice("i-5", 6, new DateOnly(2025, 12, 31), new InvoiceLine("B", 1, 100m, VatRate.Zero));
        var draft = Invoice.CreateDraft("i-6", "t-1", "c-1", [new InvoiceLine("C", 1, 100m, VatRate.Zero)], Now);

        var sie = SieExporter.Generate([inYear, otherYear, draft], Seller(), 2026, Now);

        Assert.Contains("#VER \"F\" \"5\"", sie);
        Assert.DoesNotContain("#VER \"F\" \"6\"", sie);
        Assert.DoesNotContain("i-6", sie);
    }

    [Fact]
    public void Account_declarations_only_include_used_accounts()
    {
        var invoice = SentInvoice("i-7", 7, new DateOnly(2026, 2, 2), new InvoiceLine("X", 1, 100m, VatRate.TwentyFive));

        var sie = SieExporter.Generate([invoice], Seller(), 2026, Now);

        Assert.Contains("#KONTO 1510", sie);
        Assert.Contains("#KONTO 3001", sie);
        Assert.Contains("#KONTO 2611", sie);
        Assert.DoesNotContain("#KONTO 3002", sie);
        Assert.DoesNotContain("#KONTO 2621", sie);
    }
}
