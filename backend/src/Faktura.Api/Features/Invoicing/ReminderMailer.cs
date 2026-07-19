using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Emailing;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Delad utskickskärna för betalningspåminnelser — används av både den manuella tjänsten och
/// det automatiska jobbet. Tar tenantId explicit (ingen ITenantContext) så den fungerar i
/// systemkontext; bygger mejl (text + original-PDF), skickar och loggar med ordningsnummer.
/// </summary>
public sealed class ReminderMailer
{
    private readonly IOrganizationRepository _organizations;
    private readonly IInvoiceReminderRepository _reminders;
    private readonly IInvoicePdfGenerator _pdf;
    private readonly IEmailSender _sender;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<ReminderMailer> _logger;
    private readonly SmtpOptions _smtp;

    public ReminderMailer(IOrganizationRepository organizations, IInvoiceReminderRepository reminders,
        IInvoicePdfGenerator pdf, IEmailSender sender, IIdGenerator ids, IClock clock,
        ILogger<ReminderMailer> logger, IOptions<SmtpOptions> smtp)
    {
        _organizations = organizations;
        _reminders = reminders;
        _pdf = pdf;
        _sender = sender;
        _ids = ids;
        _clock = clock;
        _logger = logger;
        _smtp = smtp.Value;
    }

    /// <summary>Skickar och loggar en påminnelse. Returnerar den loggade posten; fel loggas som Failed.</summary>
    public async Task<Result<InvoiceReminder>> SendAsync(
        string tenantId, Invoice invoice, string recipient, ReminderType type, string? replyTo, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(tenantId, ct);
        var sellerName = org?.Name ?? "";

        var history = await _reminders.ListByInvoiceAsync(tenantId, invoice.Id, ct);
        var sequence = history.Count(r => r.Status == ReminderStatus.Sent) + 1;

        var subject = $"Påminnelse {sequence}: Faktura {invoice.Number} från {sellerName}";
        var body =
            $"Hej,\n\nDetta är påminnelse nr {sequence} om faktura {invoice.Number} från {sellerName}, " +
            $"som förföll {invoice.DueDate:yyyy-MM-dd}. Belopp att betala: {invoice.Totals.Gross} kr.\n" +
            $"Fakturan bifogas som PDF. Har du redan betalat kan du bortse från denna påminnelse.\n\n" +
            $"Vänliga hälsningar,\n{sellerName}";

        var message = new EmailMessage(
            FromAddress: _smtp.FromAddress,
            FromDisplayName: sellerName,
            ReplyTo: replyTo,
            To: recipient,
            Subject: subject,
            Body: body,
            Attachment: new EmailAttachment($"faktura-{invoice.Number}.pdf", "application/pdf",
                _pdf.Generate(invoice, org)));

        try
        {
            await _sender.SendAsync(message, ct);
            var log = InvoiceReminder.Sent(_ids.NewId(), tenantId, invoice.Id, type, recipient, subject, sequence, _clock.UtcNow);
            await _reminders.AddAsync(log, ct);
            _logger.LogInformation("Reminder {Sequence} sent for invoice {InvoiceId} (tenant {TenantId})", sequence, invoice.Id, tenantId);
            return Result.Success(log);
        }
        catch (Exception ex)
        {
            var log = InvoiceReminder.Failed(_ids.NewId(), tenantId, invoice.Id, type, recipient, subject, sequence, ex.Message, _clock.UtcNow);
            await _reminders.AddAsync(log, ct);
            _logger.LogWarning(ex, "Reminder failed for invoice {InvoiceId} (tenant {TenantId})", invoice.Id, tenantId);
            return Result.Failure<InvoiceReminder>(Error.EmailFailed());
        }
    }
}
