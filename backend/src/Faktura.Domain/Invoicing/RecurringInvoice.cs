using Faktura.Domain.Common;

namespace Faktura.Domain.Invoicing;

public enum RecurrenceInterval { Monthly = 0, Quarterly = 1, Yearly = 2 }
public enum RecurringStatus { Active = 0, Paused = 1 }

/// <summary>
/// Mall för en återkommande faktura. Jobbet genererar en riktig faktura varje gång
/// <see cref="NextRunDate"/> passeras; raderna kopieras (snapshot) till den genererade fakturan.
/// </summary>
public sealed class RecurringInvoice
{
    private readonly List<InvoiceLine> _lines;

    public string Id { get; }
    public string TenantId { get; }
    public string CustomerId { get; private set; }
    public RecurrenceInterval Interval { get; private set; }
    public RecurringStatus Status { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly NextRunDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;
    public InvoiceTotals Totals => InvoiceCalculator.Compute(_lines);

    public RecurringInvoice(string id, string tenantId, string customerId, RecurrenceInterval interval,
        RecurringStatus status, DateOnly startDate, DateOnly nextRunDate, DateOnly? endDate,
        IEnumerable<InvoiceLine> lines, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        Interval = interval;
        Status = status;
        StartDate = startDate;
        NextRunDate = nextRunDate;
        EndDate = endDate;
        _lines = lines.ToList();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Result<RecurringInvoice> CreateNew(string id, string tenantId, string customerId,
        RecurrenceInterval interval, DateOnly startDate, DateOnly? endDate, IEnumerable<InvoiceLine> lines, DateTime now)
    {
        var list = lines.ToList();
        if (list.Count == 0) return Result.Failure<RecurringInvoice>(Error.EmptyInvoice());
        if (endDate is { } end && end < startDate)
            return Result.Failure<RecurringInvoice>(Error.Validation("Slutdatum kan inte vara före startdatum."));

        return Result.Success(new RecurringInvoice(id, tenantId, customerId, interval,
            RecurringStatus.Active, startDate, nextRunDate: startDate, endDate, list, now, now));
    }

    public Result Update(string customerId, RecurrenceInterval interval, DateOnly? endDate, IEnumerable<InvoiceLine> lines, DateTime now)
    {
        var list = lines.ToList();
        if (list.Count == 0) return Result.Failure(Error.EmptyInvoice());

        CustomerId = customerId;
        Interval = interval;
        EndDate = endDate;
        _lines.Clear();
        _lines.AddRange(list);
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Förfallen för generering: aktiv, nästa körning passerad och inte bortom slutdatum.</summary>
    public bool IsDue(DateOnly today) =>
        Status == RecurringStatus.Active
        && NextRunDate <= today
        && (EndDate is not { } end || NextRunDate <= end);

    /// <summary>Flyttar fram nästa körning en period. DateOnly.AddMonths klampar månadsslut (31 jan → 28/29 feb).</summary>
    public void AdvanceNextRun(DateTime now)
    {
        NextRunDate = Interval switch
        {
            RecurrenceInterval.Monthly => NextRunDate.AddMonths(1),
            RecurrenceInterval.Quarterly => NextRunDate.AddMonths(3),
            _ => NextRunDate.AddYears(1),
        };
        UpdatedAt = now;
    }

    public void Pause(DateTime now) { Status = RecurringStatus.Paused; UpdatedAt = now; }
    public void Resume(DateTime now) { Status = RecurringStatus.Active; UpdatedAt = now; }
}
