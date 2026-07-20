using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class OcrReferenceTests
{
    [Theory]
    [InlineData(1, "133")]       // bas "1" + längdsiffra 3 + Luhn 3
    [InlineData(42, "4242")]     // bas "42" + längdsiffra 4 + Luhn 2
    [InlineData(1234567, "123456790")]
    public void Generate_appends_length_digit_and_luhn_check(long number, string expected)
        => Assert.Equal(expected, OcrReference.Generate(number));

    [Theory]
    [InlineData("133", true)]
    [InlineData("4242", true)]
    [InlineData("123456790", true)]
    [InlineData("134", false)]   // fel kontrollsiffra
    [InlineData("1330", false)]  // fel längdsiffra
    [InlineData("13a", false)]   // icke-siffror
    [InlineData("", false)]
    public void IsValid_checks_luhn_and_length_digit(string ocr, bool expected)
        => Assert.Equal(expected, OcrReference.IsValid(ocr));
}

public class InvoicePaymentTests
{
    private static readonly DateTime Now = new(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 19);

    private static Invoice SentInvoice() // 1 × 1000 @ 25 % ⇒ brutto 1250
    {
        var inv = Invoice.CreateDraft("i-1", "t-1", "c-1", [new InvoiceLine("Konsult", 1, 1000m, VatRate.TwentyFive)], Now);
        inv.Send(1, Today, new CustomerSnapshot("Kund AB", null, null, null, null), 30, Now);
        return inv;
    }

    [Fact]
    public void Send_assigns_ocr_number()
    {
        var inv = SentInvoice();
        Assert.Equal(OcrReference.Generate(1), inv.OcrNumber);
        Assert.True(OcrReference.IsValid(inv.OcrNumber!));
    }

    [Fact]
    public void Partial_payment_reduces_remaining_but_keeps_sent()
    {
        var inv = SentInvoice();

        var result = inv.RegisterPayment(500m, Today, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Sent, inv.Status);
        Assert.Equal(500m, inv.PaidAmount);
        Assert.Equal(750m, inv.RemainingAmount);
        Assert.Null(inv.PaidDate);
    }

    [Fact]
    public void Final_payment_marks_paid_with_that_date()
    {
        var inv = SentInvoice();
        inv.RegisterPayment(500m, Today, Now);

        var final = inv.RegisterPayment(750m, new DateOnly(2026, 8, 1), Now);

        Assert.True(final.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Equal(0m, inv.RemainingAmount);
        Assert.Equal(new DateOnly(2026, 8, 1), inv.PaidDate);
    }

    [Fact]
    public void Overpayment_is_rejected()
    {
        var inv = SentInvoice();
        var result = inv.RegisterPayment(1251m, Today, Now);
        Assert.Equal("over_payment", result.Error.Code);
    }

    [Fact]
    public void Non_positive_or_wrong_state_is_rejected()
    {
        var inv = SentInvoice();
        Assert.True(inv.RegisterPayment(0m, Today, Now).IsFailure);

        var draft = Invoice.CreateDraft("i-2", "t-1", "c-1", [new InvoiceLine("X", 1, 100m, VatRate.Zero)], Now);
        Assert.Equal("invalid_state", draft.RegisterPayment(50m, Today, Now).Error.Code);
    }

    [Fact]
    public void MarkPaid_registers_full_remaining_amount()
    {
        var inv = SentInvoice();
        inv.RegisterPayment(250m, Today, Now);

        Assert.True(inv.MarkPaid(Today, Now).IsSuccess);

        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Equal(1250m, inv.PaidAmount);
        Assert.Equal(0m, inv.RemainingAmount);
    }
}

public class PartialCreditTests
{
    private static readonly DateTime Now = new(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 19);

    private static Invoice SentInvoice() // 10×1000 @25 % + 4×500 @6 % ⇒ brutto 12500 + 2120 = 14620
    {
        var inv = Invoice.CreateDraft("i-1", "t-1", "c-1",
            [new InvoiceLine("Konsult", 10, 1000m, VatRate.TwentyFive), new InvoiceLine("Resa", 4, 500m, VatRate.Six)], Now);
        inv.Send(1, Today, new CustomerSnapshot("Kund AB", null, null, null, null), 30, Now);
        return inv;
    }

    [Fact]
    public void Selected_lines_are_negated_with_chosen_quantity()
    {
        var inv = SentInvoice();

        var lines = inv.BuildCreditLines([new CreditSelection(0, 2)]);

        Assert.True(lines.IsSuccess);
        var line = Assert.Single(lines.Value);
        Assert.Equal(-2m, line.Quantity);
        Assert.Equal(1000m, line.UnitPriceExclVat);

        var note = Invoice.CreateCreditNote("cn-1", inv, 2, Today, Now, lines.Value);
        Assert.Equal(-2500m, note.Totals.Gross.Amount); // 2 × 1000 × 1,25
    }

    [Fact]
    public void No_selection_means_full_credit()
    {
        var inv = SentInvoice();
        var lines = inv.BuildCreditLines(null);
        Assert.True(lines.IsSuccess);
        Assert.Equal(2, lines.Value.Count);
        Assert.Equal(-10m, lines.Value[0].Quantity);
    }

    [Theory]
    [InlineData(2, 1)]    // index utanför raderna
    [InlineData(0, 0)]    // antal måste vara > 0
    [InlineData(0, 11)]   // antal över radens antal
    public void Invalid_selection_is_rejected(int index, double quantity)
    {
        var inv = SentInvoice();
        var result = inv.BuildCreditLines([new CreditSelection(index, (decimal)quantity)]);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Credit_beyond_remaining_creditable_is_rejected()
    {
        var inv = SentInvoice();
        inv.RegisterCredit(13000m, Now); // kvar att kreditera: 1620

        var result = inv.BuildCreditLines([new CreditSelection(0, 2)]); // 2500 > 1620

        Assert.Equal("over_credit", result.Error.Code);
    }

    [Fact]
    public void Draft_cannot_be_credited()
    {
        var draft = Invoice.CreateDraft("i-2", "t-1", "c-1", [new InvoiceLine("X", 1, 100m, VatRate.Zero)], Now);
        Assert.Equal("invalid_state", draft.BuildCreditLines(null).Error.Code);
    }

    [Fact]
    public void Share_token_only_for_numbered_invoices_and_is_stable()
    {
        var draft = Invoice.CreateDraft("i-3", "t-1", "c-1", [new InvoiceLine("X", 1, 100m, VatRate.Zero)], Now);
        Assert.Equal("invalid_state", draft.AssignShareToken("abc", Now).Error.Code);

        draft.Send(1, Today, new CustomerSnapshot("Kund AB", null, null, null, null), 30, Now);
        Assert.True(draft.AssignShareToken("first", Now).IsSuccess);
        Assert.True(draft.AssignShareToken("second", Now).IsSuccess); // idempotent
        Assert.Equal("first", draft.ShareToken);

        var token1 = ShareTokens.New();
        Assert.Equal(32, token1.Length);
        Assert.NotEqual(token1, ShareTokens.New());
    }

    [Fact]
    public void Dashboard_outstanding_uses_remaining_after_partial_payment()
    {
        var inv = Invoice.CreateDraft("i-1", "t-1", "c-1", [new InvoiceLine("Konsult", 1, 1000m, VatRate.TwentyFive)], Now);
        inv.Send(1, Today, new CustomerSnapshot("Kund AB", null, null, null, null), 30, Now); // brutto 1250
        inv.RegisterPayment(1000m, Today, Now);

        var figures = DashboardCalculator.Compute([inv], Today);

        Assert.Equal(250m, figures.Outstanding);
    }
}
