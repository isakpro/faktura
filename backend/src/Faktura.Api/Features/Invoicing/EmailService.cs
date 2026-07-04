using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Faktura.Domain.Emailing;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Mejlar en skickad faktura som PDF-bilaga och loggar varje utskick (tenant-scoped).</summary>
public sealed class EmailService
{
    private readonly ITenantContext _tenant;
    private readonly IInvoiceRepository _invoices;
    private readonly IOrganizationRepository _organizations;
    private readonly IInvoicePdfGenerator _pdf;
    private readonly IEmailSender _sender;
    private readonly IInvoiceEmailRepository _log;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpOptions _smtp;

    public EmailService(ITenantContext tenant, IInvoiceRepository invoices, IOrganizationRepository organizations,
        IInvoicePdfGenerator pdf, IEmailSender sender, IInvoiceEmailRepository log, IIdGenerator ids, IClock clock,
        ILogger<EmailService> logger, IOptions<SmtpOptions> smtp)
    {
        _tenant = tenant;
        _invoices = invoices;
        _organizations = organizations;
        _pdf = pdf;
        _sender = sender;
        _log = log;
        _ids = ids;
        _clock = clock;
        _logger = logger;
        _smtp = smtp.Value;
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
        var recipient = email.Value.Value;

        var org = await _organizations.GetByIdAsync(_tenant.TenantId, ct);
        var sellerName = org?.Name ?? "";
        var docName = invoice.Type == InvoiceType.CreditNote ? "Kreditfaktura" : "Faktura";
        var subject = $"{docName} {invoice.Number} från {sellerName}";
        var body =
            $"Hej,\n\nHär kommer {docName.ToLowerInvariant()} {invoice.Number} från {sellerName}. " +
            $"Belopp: {invoice.Totals.Gross} kr. Dokumentet finns som PDF-bilaga.\n\nVänliga hälsningar,\n{sellerName}";

        var pdfBytes = _pdf.Generate(invoice, sellerName);
        var message = new EmailMessage(
            FromAddress: _smtp.FromAddress,
            FromDisplayName: sellerName,
            ReplyTo: _tenant.Email,
            To: recipient,
            Subject: subject,
            Body: body,
            Attachment: new EmailAttachment($"{docName.ToLowerInvariant()}-{invoice.Number}.pdf", "application/pdf", pdfBytes));

        try
        {
            await _sender.SendAsync(message, ct);
            var log = InvoiceEmail.Sent(_ids.NewId(), _tenant.TenantId, invoice.Id, recipient, subject, _clock.UtcNow);
            await _log.AddAsync(log, ct);
            _logger.LogInformation("Invoice {InvoiceId} emailed to {Recipient} (tenant {TenantId})", invoice.Id, recipient, _tenant.TenantId);
            return Result.Success(ToDto(log));
        }
        catch (Exception ex)
        {
            // Leveransfel: logga som misslyckat men lämna fakturan orörd.
            var log = InvoiceEmail.Failed(_ids.NewId(), _tenant.TenantId, invoice.Id, recipient, subject, ex.Message, _clock.UtcNow);
            await _log.AddAsync(log, ct);
            _logger.LogWarning(ex, "Failed to email invoice {InvoiceId} to {Recipient}", invoice.Id, recipient);
            return Result.Failure<InvoiceEmailDto>(Error.EmailFailed());
        }
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
