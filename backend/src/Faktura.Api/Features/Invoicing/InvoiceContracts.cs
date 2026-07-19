namespace Faktura.Api.Features.Invoicing;

public sealed record InvoiceLineInput(string Description, decimal Quantity, decimal UnitPriceExclVat, int VatRate, string? Unit = null);

public sealed record CreateInvoiceRequest(string CustomerId, List<InvoiceLineInput> Lines);
public sealed record UpdateInvoiceRequest(string CustomerId, List<InvoiceLineInput> Lines);
public sealed record MarkPaidRequest(DateOnly PaidDate);

// Betalningsreskontra & delkreditering (spec 012).
public sealed record RegisterPaymentRequest(decimal Amount, DateOnly? PaidDate = null, string? Note = null);
public sealed record PaymentDto(string Id, decimal Amount, DateOnly PaidDate, string? Note, DateTime CreatedAt);
public sealed record CreditLineInput(int Index, decimal Quantity);
public sealed record CreditRequest(List<CreditLineInput>? Lines = null);

public sealed record InvoiceLineDto(
    string Description, decimal Quantity, decimal UnitPriceExclVat, int VatRate, decimal Net, decimal Vat, string? Unit = null);

public sealed record VatByRateDto(int Rate, decimal Vat);
public sealed record InvoiceTotalsDto(decimal Net, IReadOnlyList<VatByRateDto> VatByRate, decimal Gross);

public sealed record InvoiceDto(
    string Id,
    string Type,
    string Status,
    long? Number,
    string CustomerId,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    DateOnly? PaidDate,
    string? OriginalInvoiceId,
    IReadOnlyList<InvoiceLineDto> Lines,
    InvoiceTotalsDto Totals,
    string? OcrNumber = null,
    decimal PaidAmount = 0m,
    decimal RemainingAmount = 0m);

public sealed record InvoiceListItemDto(
    string Id, long? Number, string Status, string CustomerId, decimal Gross, DateOnly? DueDate);
