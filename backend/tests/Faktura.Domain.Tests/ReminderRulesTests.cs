using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class ReminderRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc);

    private static Invoice Sent(DateOnly invoiceDate, int termsDays = 30)
    {
        var inv = Invoice.CreateDraft("i-1", "t-1", "c-1",
            [new InvoiceLine("Rad", 1, 1000m, VatRate.TwentyFive)], Now);
        inv.Send(1, invoiceDate, new CustomerSnapshot("Kund", "k@x.se", null, null, null), termsDays, Now);
        return inv;
    }

    [Fact]
    public void Overdue_invoice_can_be_reminded()
    {
        var inv = Sent(new DateOnly(2026, 1, 1)); // förfaller 2026-01-31
        Assert.True(ReminderRules.CanRemind(inv, new DateOnly(2026, 3, 1)).IsSuccess);
    }

    [Fact]
    public void Not_yet_due_invoice_is_rejected()
    {
        var inv = Sent(new DateOnly(2026, 6, 20)); // förfaller 2026-07-20
        var result = ReminderRules.CanRemind(inv, new DateOnly(2026, 7, 4));
        Assert.True(result.IsFailure);
        Assert.Equal("invalid_state", result.Error.Code);
    }

    [Fact]
    public void Draft_and_paid_are_rejected()
    {
        var draft = Invoice.CreateDraft("i", "t", "c", [new InvoiceLine("R", 1, 1m, VatRate.Zero)], Now);
        Assert.True(ReminderRules.CanRemind(draft, new DateOnly(2026, 7, 4)).IsFailure);

        var paid = Sent(new DateOnly(2026, 1, 1));
        paid.MarkPaid(new DateOnly(2026, 3, 1), Now);
        Assert.True(ReminderRules.CanRemind(paid, new DateOnly(2026, 4, 1)).IsFailure);
    }

    [Fact]
    public void Credit_note_is_rejected()
    {
        var original = Sent(new DateOnly(2026, 1, 1));
        var credit = Invoice.CreateCreditNote("cn", original, 2, new DateOnly(2026, 2, 1), Now);
        Assert.True(ReminderRules.CanRemind(credit, new DateOnly(2026, 6, 1)).IsFailure);
    }

    [Theory]
    [InlineData("2026-02-06", 7, false)] // förfallen 6 dagar (due 31 jan) — under gränsen
    [InlineData("2026-02-07", 7, true)]  // exakt 7 dagar
    [InlineData("2026-03-01", 7, true)]  // långt förbi
    public void QualifiesForAutomatic_respects_days_after_due(string todayStr, int days, bool expected)
    {
        var inv = Sent(new DateOnly(2026, 1, 1)); // förfaller 2026-01-31
        Assert.Equal(expected, ReminderRules.QualifiesForAutomatic(inv, DateOnly.Parse(todayStr), days));
    }
}
