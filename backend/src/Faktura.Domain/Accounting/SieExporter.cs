using System.Text;
using Faktura.Domain.Invoicing;
using Faktura.Domain.Organizations;

namespace Faktura.Domain.Accounting;

/// <summary>
/// Genererar en SIE4-fil (svenskt standardformat för bokföringsdata) av ett räkenskapsårs
/// fakturor, för import i extern bokföringsprogramvara (spec 015). Ren text — ingen I/O;
/// anropande lager avgör kodning/filnamn. Bokför mot en inbyggd, ej konfigurerbar kontoplan:
/// varje verifikation debiterar kundfordringar med bruttot och krediterar försäljning
/// (per momssats) + utgående moms — alltid i balans. Kreditfakturors redan negerade
/// radbelopp ger automatiskt korrekt motbokning.
/// </summary>
public static class SieExporter
{
    private const string AccountsReceivable = "1510";

    private static string SalesAccount(int vatPercent) => vatPercent switch
    {
        25 => "3001",
        12 => "3002",
        6 => "3003",
        _ => "3004"
    };

    private static string? VatAccount(int vatPercent) => vatPercent switch
    {
        25 => "2611",
        12 => "2621",
        6 => "2631",
        _ => null // momsfritt saknar utgående moms-konto
    };

    private static readonly IReadOnlyDictionary<string, string> AccountNames = new Dictionary<string, string>
    {
        [AccountsReceivable] = "Kundfordringar",
        ["3001"] = "Försäljning 25% moms",
        ["3002"] = "Försäljning 12% moms",
        ["3003"] = "Försäljning 6% moms",
        ["3004"] = "Försäljning momsfri",
        ["2611"] = "Utgående moms 25%",
        ["2621"] = "Utgående moms 12%",
        ["2631"] = "Utgående moms 6%",
    };

    public static string Generate(IEnumerable<Invoice> invoices, Organization? org, int year, DateTime generatedAt)
    {
        var included = invoices
            .Where(i => i.Type is InvoiceType.Invoice or InvoiceType.CreditNote)
            .Where(i => i.Status != InvoiceStatus.Draft)
            .Where(i => i.InvoiceDate?.Year == year)
            .OrderBy(i => i.Number)
            .ToList();

        var usedAccounts = new SortedSet<string> { AccountsReceivable };
        var verifications = new List<string>();

        foreach (var invoice in included)
        {
            var lines = BuildVerificationLines(invoice, usedAccounts);
            var docType = invoice.Type == InvoiceType.CreditNote ? "Kreditfaktura" : "Faktura";
            var text = Escape($"{docType} {invoice.Number} {invoice.CustomerSnapshot?.Name}".Trim());

            var sb = new StringBuilder();
            sb.Append($"#VER \"F\" \"{invoice.Number}\" {invoice.InvoiceDate:yyyyMMdd} \"{text}\"\r\n{{\r\n");
            foreach (var (account, amount) in lines)
                sb.Append($"#TRANS {account} {{}} {amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}\r\n");
            sb.Append("}\r\n");
            verifications.Add(sb.ToString());
        }

        return BuildHeader(org, year, generatedAt, usedAccounts) + string.Join("", verifications);
    }

    private static List<(string Account, decimal Amount)> BuildVerificationLines(Invoice invoice, SortedSet<string> usedAccounts)
    {
        var lines = new List<(string, decimal)>
        {
            (AccountsReceivable, invoice.Totals.Gross.Amount) // positivt = debet, negativt = kredit
        };

        foreach (var group in invoice.Lines.GroupBy(l => (int)l.VatRate))
        {
            var net = group.Sum(l => l.Net.Amount);
            var vat = group.Sum(l => l.Vat.Amount);

            var salesAccount = SalesAccount(group.Key);
            usedAccounts.Add(salesAccount);
            lines.Add((salesAccount, -net));

            if (vat != 0m)
            {
                var vatAccount = VatAccount(group.Key)!;
                usedAccounts.Add(vatAccount);
                lines.Add((vatAccount, -vat));
            }
        }

        return lines;
    }

    private static string BuildHeader(Organization? org, int year, DateTime generatedAt, IEnumerable<string> usedAccounts)
    {
        var sb = new StringBuilder();
        sb.Append("#FLAGGA 0\r\n");
        sb.Append("#PROGRAM \"Faktura\" 1.0\r\n");
        sb.Append("#FORMAT PC8\r\n");
        sb.Append($"#GEN {generatedAt:yyyyMMdd}\r\n");
        sb.Append("#SIETYP 4\r\n");
        sb.Append($"#FNAMN \"{Escape(org?.Name ?? "")}\"\r\n");
        if (org?.Profile?.OrgNumber is { } orgNr) sb.Append($"#ORGNR {orgNr}\r\n");
        sb.Append($"#RAR 0 {year}0101 {year}1231\r\n");
        foreach (var account in usedAccounts.OrderBy(a => a))
            sb.Append($"#KONTO {account} \"{AccountNames.GetValueOrDefault(account, account)}\"\r\n");
        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\"", "'");
}
