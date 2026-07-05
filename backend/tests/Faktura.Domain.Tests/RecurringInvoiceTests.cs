using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class RecurringInvoiceTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    private static RecurringInvoice New(RecurrenceInterval interval, DateOnly start, DateOnly? end = null) =>
        RecurringInvoice.CreateNew("r-1", "t-1", "c-1", interval, start, end,
            [new InvoiceLine("Abonnemang", 1, 500m, VatRate.TwentyFive)], Now).Value;

    [Fact]
    public void CreateNew_starts_active_with_next_run_at_start_date()
    {
        var r = New(RecurrenceInterval.Monthly, new DateOnly(2026, 8, 1));
        Assert.Equal(RecurringStatus.Active, r.Status);
        Assert.Equal(new DateOnly(2026, 8, 1), r.NextRunDate);
    }

    [Fact]
    public void CreateNew_rejects_empty_lines_and_end_before_start()
    {
        Assert.Equal("empty_invoice", RecurringInvoice.CreateNew("r", "t", "c",
            RecurrenceInterval.Monthly, new DateOnly(2026, 8, 1), null, [], Now).Error.Code);
        Assert.True(RecurringInvoice.CreateNew("r", "t", "c", RecurrenceInterval.Monthly,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1),
            [new InvoiceLine("X", 1, 1m, VatRate.Zero)], Now).IsFailure);
    }

    [Theory]
    [InlineData(RecurrenceInterval.Monthly, "2026-01-31", "2026-02-28")]  // månadsslut klampas
    [InlineData(RecurrenceInterval.Monthly, "2026-03-15", "2026-04-15")]
    [InlineData(RecurrenceInterval.Quarterly, "2026-01-01", "2026-04-01")]
    [InlineData(RecurrenceInterval.Yearly, "2026-06-30", "2027-06-30")]
    public void AdvanceNextRun_steps_one_period(RecurrenceInterval interval, string start, string expected)
    {
        var r = New(interval, DateOnly.Parse(start));
        r.AdvanceNextRun(Now);
        Assert.Equal(DateOnly.Parse(expected), r.NextRunDate);
    }

    [Fact]
    public void IsDue_respects_status_date_and_end_date()
    {
        var r = New(RecurrenceInterval.Monthly, new DateOnly(2026, 7, 1), end: new DateOnly(2026, 8, 15));

        Assert.True(r.IsDue(new DateOnly(2026, 7, 5)));    // passerad körning
        Assert.False(r.IsDue(new DateOnly(2026, 6, 30)));  // före start

        r.Pause(Now);
        Assert.False(r.IsDue(new DateOnly(2026, 7, 5)));   // pausad
        r.Resume(Now);

        r.AdvanceNextRun(Now); // -> 1 aug (före slutdatum 15 aug)
        Assert.True(r.IsDue(new DateOnly(2026, 9, 1)));
        r.AdvanceNextRun(Now); // -> 1 sep (efter slutdatum)
        Assert.False(r.IsDue(new DateOnly(2026, 10, 1)));  // bortom slutdatum
    }
}
