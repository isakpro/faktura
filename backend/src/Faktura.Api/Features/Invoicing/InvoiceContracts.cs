namespace Faktura.Api.Features.Invoicing;

public sealed record InvoiceLineInput(string Description, decimal Quantity, decimal UnitPriceExclVat, int VatRate);

public sealed record CreateInvoiceRequest(string CustomerId, List<InvoiceLineInput> Lines);
public sealed record UpdateInvoiceRequest(string CustomerId, List<InvoiceLineInput> Lines);
public sealed record MarkPaidRequest(DateOnly PaidDate);

public sealed record InvoiceLineDto(
    string Description, decimal Quantity, decimal UnitPriceExclVat, int VatRate, decimal Net, decimal Vat);

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
    InvoiceTotalsDto Totals);

public sealed record InvoiceListItemDto(
    string Id, long? Number, string Status, string CustomerId, decimal Gross, DateOnly? DueDate);
