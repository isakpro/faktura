using Faktura.Domain.Common;
using Faktura.Domain.Customers;

namespace Faktura.Domain.Invoicing;

/// <summary>Oföränderlig kopia av kunduppgifter, tagen när fakturan skickas.</summary>
public sealed record CustomerSnapshot(string Name, string? Email, string? OrgNumber, string? VatNumber, Address? Address);

/// <summary>Radval vid delkreditering: radindex i originalet + antal att kreditera.</summary>
public sealed record CreditSelection(int LineIndex, decimal Quantity);

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

    /// <summary>OCR-referens (spec 012), sätts vid skick. Null för utkast/äldre fakturor.</summary>
    public string? OcrNumber { get; private set; }

    /// <summary>Summa registrerade betalningar (spec 012).</summary>
    public decimal PaidAmount { get; private set; }

    /// <summary>Mallen som genererade fakturan (spårbarhet, spec 007). Null för manuella fakturor.</summary>
    public string? RecurringSourceId { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public Invoice(string id, string tenantId, string customerId, CustomerSnapshot? customerSnapshot,
        InvoiceType type, InvoiceStatus status, long? number, DateOnly? invoiceDate, DateOnly? dueDate,
        DateOnly? paidDate, string? originalInvoiceId, decimal creditedAmount, IEnumerable<InvoiceLine> lines,
        DateTime createdAt, DateTime updatedAt, string? recurringSourceId = null,
        string? ocrNumber = null, decimal paidAmount = 0m)
    {
        RecurringSourceId = recurringSourceId;
        OcrNumber = ocrNumber;
        PaidAmount = paidAmount;
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

    public static Invoice CreateDraft(string id, string tenantId, string customerId, IEnumerable<InvoiceLine> lines, DateTime now,
        string? recurringSourceId = null)
        => new(id, tenantId, customerId, customerSnapshot: null, InvoiceType.Invoice, InvoiceStatus.Draft,
            number: null, invoiceDate: null, dueDate: null, paidDate: null, originalInvoiceId: null,
            creditedAmount: 0m, lines, now, now)
        { RecurringSourceId = recurringSourceId };

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
        OcrNumber = Type == InvoiceType.Invoice ? OcrReference.Generate(number) : null;
        Status = InvoiceStatus.Sent;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Saldo kvar att betala. Betald/krediterad faktura har inget saldo (även äldre utan reskontra).</summary>
    public decimal RemainingAmount =>
        Status is InvoiceStatus.Paid or InvoiceStatus.Credited ? 0m : Totals.Gross.Amount - PaidAmount;

    /// <summary>Registrerar en betalning; vid nollsaldo blir fakturan Betald med betalningens datum.</summary>
    public Result RegisterPayment(decimal amount, DateOnly paidDate, DateTime now)
    {
        if (Type != InvoiceType.Invoice || Status != InvoiceStatus.Sent) return Result.Failure(Error.InvalidState());
        if (amount <= 0) return Result.Failure(Error.Validation("Beloppet måste vara större än noll."));
        if (amount > RemainingAmount) return Result.Failure(Error.OverPayment());

        PaidAmount += amount;
        if (Totals.Gross.Amount - PaidAmount <= 0)
        {
            Status = InvoiceStatus.Paid;
            PaidDate = paidDate;
        }
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Betalar hela kvarvarande saldot i ett svep (snabbknappen "Betald").</summary>
    public Result MarkPaid(DateOnly paidDate, DateTime now) => RegisterPayment(RemainingAmount, paidDate, now);

    /// <summary>Belopp som återstår att kreditera (brutto minus redan krediterat).</summary>
    public decimal RemainingCreditable => Totals.Gross.Amount - CreditedAmount;

    /// <summary>Registrerar en kreditering på originalfakturan; markerar Credited när fullt krediterad.</summary>
    public Result RegisterCredit(decimal amount, DateTime now)
    {
        if (Type != InvoiceType.Invoice || Status is InvoiceStatus.Draft)
            return Result.Failure(Error.InvalidState());
        if (amount <= 0 || amount > RemainingCreditable)
            return Result.Failure(Error.OverCredit());

        CreditedAmount += amount;
        if (CreditedAmount >= Totals.Gross.Amount) Status = InvoiceStatus.Credited;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Validerar radval för (del)kreditering och bygger de negerade kreditraderna.
    /// Tomt/utelämnat radval ⇒ full kreditering av samtliga rader.
    /// </summary>
    public Result<List<InvoiceLine>> BuildCreditLines(IReadOnlyList<CreditSelection>? selections)
    {
        if (Type != InvoiceType.Invoice || Status is InvoiceStatus.Draft)
            return Result.Failure<List<InvoiceLine>>(Error.InvalidState());

        List<InvoiceLine> negated;
        if (selections is null || selections.Count == 0)
        {
            negated = _lines.Select(l => new InvoiceLine(l.Description, -l.Quantity, l.UnitPriceExclVat, l.VatRate, l.Unit)).ToList();
        }
        else
        {
            negated = [];
            foreach (var sel in selections)
            {
                if (sel.LineIndex < 0 || sel.LineIndex >= _lines.Count)
                    return Result.Failure<List<InvoiceLine>>(Error.Validation($"Ogiltig rad: {sel.LineIndex}."));
                var line = _lines[sel.LineIndex];
                if (sel.Quantity <= 0 || sel.Quantity > line.Quantity)
                    return Result.Failure<List<InvoiceLine>>(Error.Validation($"Ogiltigt antal för rad {sel.LineIndex}."));
                negated.Add(new InvoiceLine(line.Description, -sel.Quantity, line.UnitPriceExclVat, line.VatRate, line.Unit));
            }
        }

        var creditGross = -InvoiceCalculator.Compute(negated).Gross.Amount;
        return creditGross > RemainingCreditable
            ? Result.Failure<List<InvoiceLine>>(Error.OverCredit())
            : Result.Success(negated);
    }

    /// <summary>
    /// Skapar en kreditfaktura för <paramref name="original"/> med eget nummer och referens till originalet.
    /// Utan <paramref name="creditLines"/> negeras samtliga rader (full kreditering).
    /// </summary>
    public static Invoice CreateCreditNote(string id, Invoice original, long number, DateOnly invoiceDate, DateTime now,
        IEnumerable<InvoiceLine>? creditLines = null)
    {
        var lines = creditLines
            ?? original.Lines.Select(l => new InvoiceLine(l.Description, -l.Quantity, l.UnitPriceExclVat, l.VatRate, l.Unit));
        return new Invoice(
            id, original.TenantId, original.CustomerId, original.CustomerSnapshot,
            InvoiceType.CreditNote, InvoiceStatus.Sent, number, invoiceDate, invoiceDate, paidDate: null,
            originalInvoiceId: original.Id, creditedAmount: 0m, lines, now, now);
    }
}
