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
}
