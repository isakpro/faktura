using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Microsoft.Extensions.Logging;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Det automatiska påminnelsejobbet (systemkontext — kör över alla organisationer med
/// automatiken påslagen). Feltolerant per faktura; skickar aldrig mer än en automatisk
/// påminnelse per faktura (FR-008). Körs av <see cref="ReminderBackgroundService"/> och
/// direkt i tester.
/// </summary>
public sealed class ReminderJob
{
    private readonly IReminderSettingsRepository _settings;
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceReminderRepository _reminders;
    private readonly ReminderMailer _mailer;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(IReminderSettingsRepository settings, IInvoiceRepository invoices,
        IInvoiceReminderRepository reminders, ReminderMailer mailer, IIdGenerator ids, IClock clock,
        ILogger<ReminderJob> logger)
    {
        _settings = settings;
        _invoices = invoices;
        _reminders = reminders;
        _mailer = mailer;
        _ids = ids;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Kör en jobbomgång. Returnerar antal skickade automatiska påminnelser.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var sentCount = 0;

        foreach (var settings in await _settings.ListAutoEnabledAsync(ct))
        {
            var invoices = await _invoices.ListByTenantAsync(settings.TenantId, ct);
            foreach (var invoice in invoices.Where(i => ReminderRules.QualifiesForAutomatic(i, today, settings.DaysAfterDue)))
            {
                try
                {
                    // Dubblettskydd: max en automatisk påminnelse-post per faktura.
                    if (await _reminders.HasAutomaticAsync(settings.TenantId, invoice.Id, ct))
                        continue;

                    var recipient = invoice.CustomerSnapshot?.Email;
                    if (string.IsNullOrWhiteSpace(recipient))
                    {
                        // Loggas som misslyckad (spårbarhet) — räknas som "behandlad" så den inte spammar loggen dagligen.
                        await _reminders.AddAsync(InvoiceReminder.Failed(
                            _ids.NewId(), settings.TenantId, invoice.Id, ReminderType.Automatic,
                            recipient: "", subject: $"Påminnelse: Faktura {invoice.Number}",
                            sequence: 0, error: "Kunden saknar e-postadress.", _clock.UtcNow), ct);
                        continue;
                    }

                    var result = await _mailer.SendAsync(settings.TenantId, invoice, recipient, ReminderType.Automatic, replyTo: null, ct);
                    if (result.IsSuccess) sentCount++;
                }
                catch (Exception ex)
                {
                    // Ett fel för en faktura får inte stoppa jobbet för övriga (FR-009).
                    _logger.LogWarning(ex, "Reminder job failed for invoice {InvoiceId} (tenant {TenantId})",
                        invoice.Id, settings.TenantId);
                }
            }
        }

        _logger.LogInformation("Reminder job finished: {Count} automatic reminders sent", sentCount);
        return sentCount;
    }
}
