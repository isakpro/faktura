using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Genererar fakturor från förfallna återkommande mallar (systemkontext, som ReminderJob):
/// skapa → skicka (atomiskt nummer, kundögonblicksbild, betalvillkor) → mejla PDF. Ikappkörning
/// genererar alla missade perioder (skyddstak); feltolerant per mall; inga dubbletter vid
/// omkörning eftersom NextRunDate flyttas fram i samma varv som fakturan sparas.
/// </summary>
public sealed class RecurringInvoiceJob
{
    private const int MaxCatchUpPerTemplate = 24;

    private readonly IRecurringInvoiceRepository _recurring;
    private readonly IInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IInvoiceNumberSequence _numbers;
    private readonly InvoiceMailer _mailer;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<RecurringInvoiceJob> _logger;

    public RecurringInvoiceJob(IRecurringInvoiceRepository recurring, IInvoiceRepository invoices,
        ICustomerRepository customers, IInvoiceNumberSequence numbers, InvoiceMailer mailer,
        IIdGenerator ids, IClock clock, ILogger<RecurringInvoiceJob> logger)
    {
        _recurring = recurring;
        _invoices = invoices;
        _customers = customers;
        _numbers = numbers;
        _mailer = mailer;
        _ids = ids;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Kör en jobbomgång. Returnerar antal genererade fakturor.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var generated = 0;

        foreach (var template in await _recurring.ListDueAsync(today, ct))
        {
            try
            {
                var customer = await _customers.GetByIdAsync(template.TenantId, template.CustomerId, ct);
                if (customer is null)
                {
                    _logger.LogWarning("Recurring {Id}: customer missing — skipping (tenant {TenantId})", template.Id, template.TenantId);
                    continue;
                }

                var catchUp = 0;
                while (template.IsDue(today) && catchUp++ < MaxCatchUpPerTemplate)
                {
                    var invoice = Invoice.CreateDraft(_ids.NewId(), template.TenantId, template.CustomerId,
                        template.Lines, _clock.UtcNow, recurringSourceId: template.Id);

                    var number = await _numbers.NextAsync(template.TenantId, ct);
                    var snapshot = new CustomerSnapshot(customer.Name, customer.Email, customer.OrgNumber,
                        customer.VatNumber, customer.Address);
                    invoice.Send(number, today, snapshot, customer.PaymentTermsDays, _clock.UtcNow);
                    await _invoices.AddAsync(invoice, ct);

                    // Mejla PDF:en; kund utan e-post loggas som misslyckat utskick (fakturan är ändå skickad).
                    if (string.IsNullOrWhiteSpace(customer.Email))
                        await _mailer.LogFailureAsync(template.TenantId, invoice, "Kunden saknar e-postadress.", ct);
                    else
                        await _mailer.SendAsync(template.TenantId, invoice, customer.Email, replyTo: null, ct);

                    template.AdvanceNextRun(_clock.UtcNow);
                    await _recurring.UpdateAsync(template, ct);
                    generated++;
                }
            }
            catch (Exception ex)
            {
                // Ett fel för en mall får inte stoppa jobbet för övriga.
                _logger.LogWarning(ex, "Recurring job failed for template {Id} (tenant {TenantId})", template.Id, template.TenantId);
            }
        }

        _logger.LogInformation("Recurring job finished: {Count} invoices generated", generated);
        return generated;
    }
}

/// <summary>Kör <see cref="RecurringInvoiceJob"/> vid uppstart och därefter dagligen (ej i Testing).</summary>
public sealed class RecurringBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecurringBackgroundService> _logger;

    public RecurringBackgroundService(IServiceScopeFactory scopes, ILogger<RecurringBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RecurringInvoiceJob>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurring background run failed; retrying at next interval");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
