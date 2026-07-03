using Faktura.Domain.Common;
using Faktura.Domain.Customers;

namespace Faktura.Domain.Invoicing;

/// <summary>Oföränderlig kopia av kunduppgifter, tagen när fakturan skickas.</summary>
public sealed record CustomerSnapshot(string Name, string? Email, string? OrgNumber, string? VatNumber, Address? Address);

/// <summary>
/// Fakturaaggregat. Utkast kan redigeras fritt; vid skick tilldelas ett löpande nummer och
/// fakturan blir oföränderlig. Summor härleds via <see cref="InvoiceCalculator"/>.
/// </summary>
public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines;

    public string Id { get; }
    public string TenantId { get; }
    public string CustomerId { get; private set; }
    public CustomerSnapshot? CustomerSnapshot { get; private set; }
    public InvoiceType Type { get; }
    public InvoiceStatus Status { get; private set; }
    public long? Number { get; private set; }
    public DateOnly? InvoiceDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public string? OriginalInvoiceId { get; }
    public decimal CreditedAmount { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public Invoice(string id, string tenantId, string customerId, CustomerSnapshot? customerSnapshot,
        InvoiceType type, InvoiceStatus status, long? number, DateOnly? invoiceDate, DateOnly? dueDate,
        DateOnly? paidDate, string? originalInvoiceId, decimal creditedAmount, IEnumerable<InvoiceLine> lines,
        DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        CustomerSnapshot = customerSnapshot;
        Type = type;
        Status = status;
        Number = number;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        PaidDate = paidDate;
        OriginalInvoiceId = originalInvoiceId;
        CreditedAmount = creditedAmount;
        _lines = lines.ToList();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Invoice CreateDraft(string id, string tenantId, string customerId, IEnumerable<InvoiceLine> lines, DateTime now)
        => new(id, tenantId, customerId, customerSnapshot: null, InvoiceType.Invoice, InvoiceStatus.Draft,
            number: null, invoiceDate: null, dueDate: null, paidDate: null, originalInvoiceId: null,
            creditedAmount: 0m, lines, now, now);

    public InvoiceTotals Totals => InvoiceCalculator.Compute(_lines);

    /// <summary>Härledd: skickad, obetald och förfallodatum passerat.</summary>
    public bool IsOverdue(DateOnly today) => Status == InvoiceStatus.Sent && DueDate is { } due && due < today;

    public Result ReplaceLines(IEnumerable<InvoiceLine> lines, DateTime now)
    {
        if (Status != InvoiceStatus.Draft) return Result.Failure(Error.InvoiceLocked());
        _lines.Clear();
        _lines.AddRange(lines);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result ChangeCustomer(string customerId, DateTime now)
    {
        if (Status != InvoiceStatus.Draft) return Result.Failure(Error.InvoiceLocked());
        CustomerId = customerId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Skickar utkastet: tilldelar nummer + datum, tar kundögonblicksbild, låser.</summary>
    public Result Send(long number, DateOnly invoiceDate, CustomerSnapshot snapshot, int paymentTermsDays, DateTime now)
    {
        if (Status != InvoiceStatus.Draft) return Result.Failure(Error.InvalidState());
        if (_lines.Count == 0) return Result.Failure(Error.EmptyInvoice());

        Number = number;
        InvoiceDate = invoiceDate;
        DueDate = invoiceDate.AddDays(paymentTermsDays > 0 ? paymentTermsDays : 30);
        CustomerSnapshot = snapshot;
        Status = InvoiceStatus.Sent;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkPaid(DateOnly paidDate, DateTime now)
    {
        if (Status != InvoiceStatus.Sent) return Result.Failure(Error.InvalidState());
        PaidDate = paidDate;
        Status = InvoiceStatus.Paid;
        UpdatedAt = now;
        return Result.Success();
    }
}
