using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Emailing;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Delad kärna för att mejla en skickad faktura som PDF-bilaga (003:s mejlbyggnad).
/// Tenant-explicit (ingen ITenantContext) så den fungerar både i request-kontext
/// (EmailService) och i systemkontext (RecurringInvoiceJob). Loggar varje utskick.
/// </summary>
public sealed class InvoiceMailer
{
    private readonly IOrganizationRepository _organizations;
    private readonly IInvoiceEmailRepository _log;
    private readonly IInvoicePdfGenerator _pdf;
    private readonly IEmailSender _sender;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<InvoiceMailer> _logger;
    private readonly SmtpOptions _smtp;
    private readonly PortalLinks _portal;

    public InvoiceMailer(IOrganizationRepository organizations, IInvoiceEmailRepository log,
        IInvoicePdfGenerator pdf, IEmailSender sender, IIdGenerator ids, IClock clock,
        ILogger<InvoiceMailer> logger, IOptions<SmtpOptions> smtp, PortalLinks portal)
    {
        _portal = portal;
        _organizations = organizations;
        _log = log;
        _pdf = pdf;
        _sender = sender;
        _ids = ids;
        _clock = clock;
        _logger = logger;
        _smtp = smtp.Value;
    }

    public async Task<Result<InvoiceEmail>> SendAsync(
        string tenantId, Invoice invoice, string recipient, string? replyTo, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(tenantId, ct);
        var sellerName = org?.Name ?? "";
        var docName = invoice.Type == InvoiceType.CreditNote ? "Kreditfaktura" : "Faktura";
        var subject = $"{docName} {invoice.Number} från {sellerName}";
        var portalUrl = await _portal.EnsureAsync(invoice, ct); // null för kreditfakturor/utan BaseUrl
        var body =
            $"Hej,\n\nHär kommer {docName.ToLowerInvariant()} {invoice.Number} från {sellerName}. " +
            $"Belopp: {invoice.Totals.Gross} kr. Dokumentet finns som PDF-bilaga." +
            (portalUrl is null ? "" : $"\nVisa fakturan online: {portalUrl}") +
            $"\n\nVänliga hälsningar,\n{sellerName}";

        var message = new EmailMessage(
            FromAddress: _smtp.FromAddress,
            FromDisplayName: sellerName,
            ReplyTo: replyTo,
            To: recipient,
            Subject: subject,
            Body: body,
            Attachment: new EmailAttachment($"{docName.ToLowerInvariant()}-{invoice.Number}.pdf", "application/pdf",
                _pdf.Generate(invoice, org)));

        try
        {
            await _sender.SendAsync(message, ct);
            var log = InvoiceEmail.Sent(_ids.NewId(), tenantId, invoice.Id, recipient, subject, _clock.UtcNow);
            await _log.AddAsync(log, ct);
            _logger.LogInformation("Invoice {InvoiceId} emailed to {Recipient} (tenant {TenantId})", invoice.Id, recipient, tenantId);
            return Result.Success(log);
        }
        catch (Exception ex)
        {
            var log = InvoiceEmail.Failed(_ids.NewId(), tenantId, invoice.Id, recipient, subject, ex.Message, _clock.UtcNow);
            await _log.AddAsync(log, ct);
            _logger.LogWarning(ex, "Failed to email invoice {InvoiceId} to {Recipient}", invoice.Id, recipient);
            return Result.Failure<InvoiceEmail>(Error.EmailFailed());
        }
    }

    /// <summary>Loggar ett misslyckat utskick utan sändförsök (t.ex. kund utan e-postadress).</summary>
    public Task LogFailureAsync(string tenantId, Invoice invoice, string reason, CancellationToken ct)
        => _log.AddAsync(InvoiceEmail.Failed(_ids.NewId(), tenantId, invoice.Id,
            recipient: "", subject: $"Faktura {invoice.Number}", reason, _clock.UtcNow), ct);
}
