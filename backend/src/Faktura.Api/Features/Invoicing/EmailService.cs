using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Manuellt e-postutskick av skickad faktura (spec 003). Mejlbyggnaden delas med
/// det återkommande jobbet via <see cref="InvoiceMailer"/>.</summary>
public sealed class EmailService
{
    private readonly ITenantContext _tenant;
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceEmailRepository _log;
    private readonly InvoiceMailer _mailer;

    public EmailService(ITenantContext tenant, IInvoiceRepository invoices,
        IInvoiceEmailRepository log, InvoiceMailer mailer)
    {
        _tenant = tenant;
        _invoices = invoices;
        _log = log;
        _mailer = mailer;
    }

    public async Task<Result<InvoiceEmailDto>> SendAsync(string invoiceId, string? recipientOverride, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, invoiceId, ct);
        if (invoice is null) return Result.Failure<InvoiceEmailDto>(Error.NotFound());
        if (invoice.Number is null) return Result.Failure<InvoiceEmailDto>(Error.InvalidState()); // utkast har ingen PDF

        // Mottagare: överstyrd -> kundens (ögonblicksbild) -> fel.
        var rawRecipient = !string.IsNullOrWhiteSpace(recipientOverride)
            ? recipientOverride
            : invoice.CustomerSnapshot?.Email;
        if (string.IsNullOrWhiteSpace(rawRecipient))
            return Result.Failure<InvoiceEmailDto>(Error.NoRecipient());

        var email = EmailAddress.Create(rawRecipient);
        if (email.IsFailure) return Result.Failure<InvoiceEmailDto>(Error.InvalidRecipient());

        var sent = await _mailer.SendAsync(_tenant.TenantId, invoice, email.Value.Value, _tenant.Email, ct);
        return sent.IsSuccess ? Result.Success(ToDto(sent.Value)) : Result.Failure<InvoiceEmailDto>(sent.Error);
    }

    public async Task<Result<IReadOnlyList<InvoiceEmailDto>>> HistoryAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, invoiceId, ct);
        if (invoice is null) return Result.Failure<IReadOnlyList<InvoiceEmailDto>>(Error.NotFound());

        var items = await _log.ListByInvoiceAsync(_tenant.TenantId, invoiceId, ct);
        return Result.Success<IReadOnlyList<InvoiceEmailDto>>(items.Select(ToDto).ToList());
    }

    private static InvoiceEmailDto ToDto(InvoiceEmail e) =>
        new(e.Id, e.InvoiceId, e.Recipient, e.Subject, e.Status.ToString(), e.Error, e.SentAt);
}
