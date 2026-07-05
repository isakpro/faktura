namespace Faktura.Api.Features.Invoicing;

public sealed record RecurringInvoiceRequest(
    string CustomerId,
    List<InvoiceLineInput> Lines,
    string Interval,          // monthly | quarterly | yearly
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record RecurringInvoiceDto(
    string Id,
    string CustomerId,
    string Interval,
    string Status,
    DateOnly StartDate,
    DateOnly NextRunDate,
    DateOnly? EndDate,
    IReadOnlyList<InvoiceLineDto> Lines,
    decimal Gross);
