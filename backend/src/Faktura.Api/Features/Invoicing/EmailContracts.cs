namespace Faktura.Api.Features.Invoicing;

public sealed record SendEmailRequest(string? Recipient);

public sealed record InvoiceEmailDto(
    string Id,
    string InvoiceId,
    string Recipient,
    string Subject,
    string Status,
    string? Error,
    DateTime SentAt);
