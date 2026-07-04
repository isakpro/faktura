using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class InvoiceAggregateTests
{
    private static readonly DateTime Now = new(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 3);

    private static Invoice DraftWithLine() =>
        Invoice.CreateDraft("i-1", "t-1", "c-1", [new InvoiceLine("Konsult", 1, 1000m, VatRate.TwentyFive)], Now);

    private static CustomerSnapshot Snap() => new("Kund AB", null, null, null, null);

    [Fact]
    public void Send_assigns_number_dates_and_locks()
    {
        var inv = DraftWithLine();

        var result = inv.Send(number: 1, new DateOnly(2026, 7, 3), Snap(), paymentTermsDays: 30, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Sent, inv.Status);
        Assert.Equal(1, inv.Number);
        Assert.Equal(new DateOnly(2026, 7, 3), inv.InvoiceDate);
        Assert.Equal(new DateOnly(2026, 8, 2), inv.DueDate); // +30 dagar
        Assert.NotNull(inv.CustomerSnapshot);
    }

    [Fact]
    public void Sent_invoice_cannot_change_lines()
    {
        var inv = DraftWithLine();
        inv.Send(1, Today, Snap(), 30, Now);

        var result = inv.ReplaceLines([new InvoiceLine("Ny", 1, 5m, VatRate.Zero)], Now);

        Assert.True(result.IsFailure);
        Assert.Equal("invoice_locked", result.Error.Code);
    }

    [Fact]
    public void Cannot_send_empty_invoice()
    {
        var draft = Invoice.CreateDraft("i", "t", "c", [], Now);
        var result = draft.Send(1, Today, Snap(), 30, Now);
        Assert.Equal("empty_invoice", result.Error.Code);
    }

    [Fact]
    public void MarkPaid_only_from_sent()
    {
        var inv = DraftWithLine();
        Assert.True(inv.MarkPaid(Today, Now).IsFailure); // utkast kan ej betalas

        inv.Send(1, Today, Snap(), 30, Now);
        Assert.True(inv.MarkPaid(new DateOnly(2026, 7, 20), Now).IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
    }

    [Fact]
    public void IsOverdue_when_sent_unpaid_and_past_due()
    {
        var inv = DraftWithLine();
        inv.Send(1, new DateOnly(2026, 1, 1), Snap(), 30, Now); // förfaller 2026-01-31

        Assert.True(inv.IsOverdue(new DateOnly(2026, 3, 1)));  // förbi förfallodatum
        Assert.False(inv.IsOverdue(new DateOnly(2026, 1, 15))); // före förfallodatum

        inv.MarkPaid(new DateOnly(2026, 2, 1), Now);
        Assert.False(inv.IsOverdue(new DateOnly(2026, 3, 1)));  // betald = ej förfallen
    }

    [Fact]
    public void CreditNote_negates_lines_and_references_original()
    {
        var original = DraftWithLine(); // 1 × 1000 @ 25 % => brutto 1250
        original.Send(1, Today, Snap(), 30, Now);

        var credit = Invoice.CreateCreditNote("cn-1", original, number: 2, Today, Now);

        Assert.Equal(InvoiceType.CreditNote, credit.Type);
        Assert.Equal(InvoiceStatus.Sent, credit.Status);
        Assert.Equal(2, credit.Number);
        Assert.Equal("i-1", credit.OriginalInvoiceId);
        Assert.Equal(-1000m, credit.Totals.Net.Amount);   // negerat
        Assert.Equal(-1250m, credit.Totals.Gross.Amount);
    }

    [Fact]
    public void RegisterCredit_prevents_over_crediting()
    {
        var original = DraftWithLine(); // brutto 1250
        original.Send(1, Today, Snap(), 30, Now);

        Assert.True(original.RegisterCredit(1250m, Now).IsSuccess);
        Assert.Equal(InvoiceStatus.Credited, original.Status);

        // Andra fulla krediteringen ska nekas.
        Assert.Equal("over_credit", original.RegisterCredit(1m, Now).Error.Code);
    }
}
