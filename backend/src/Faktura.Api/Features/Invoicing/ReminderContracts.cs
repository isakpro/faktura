namespace Faktura.Api.Features.Invoicing;

public sealed record RemindRequest(string? Recipient);

public sealed record InvoiceReminderDto(
    string Id,
    string InvoiceId,
    string Type,
    string Recipient,
    string Subject,
    int Sequence,
    string Status,
    string? Error,
    DateTime SentAt);

public sealed record ReminderSettingsDto(bool AutoEnabled, int DaysAfterDue);
