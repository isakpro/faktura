using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Faktura.Domain.Invoicing;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Manuella påminnelser, historik och per-organisationsinställningar (tenant-scoped).</summary>
public sealed class ReminderService
{
    private readonly ITenantContext _tenant;
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceReminderRepository _reminders;
    private readonly IReminderSettingsRepository _settings;
    private readonly ReminderMailer _mailer;
    private readonly IClock _clock;

    public ReminderService(ITenantContext tenant, IInvoiceRepository invoices,
        IInvoiceReminderRepository reminders, IReminderSettingsRepository settings,
        ReminderMailer mailer, IClock clock)
    {
        _tenant = tenant;
        _invoices = invoices;
        _reminders = reminders;
        _settings = settings;
        _mailer = mailer;
        _clock = clock;
    }

    public async Task<Result<InvoiceReminderDto>> RemindAsync(string invoiceId, string? recipientOverride, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, invoiceId, ct);
        if (invoice is null) return Result.Failure<InvoiceReminderDto>(Error.NotFound());

        var eligible = ReminderRules.CanRemind(invoice, DateOnly.FromDateTime(_clock.UtcNow));
        if (eligible.IsFailure) return Result.Failure<InvoiceReminderDto>(eligible.Error);

        var rawRecipient = !string.IsNullOrWhiteSpace(recipientOverride)
            ? recipientOverride
            : invoice.CustomerSnapshot?.Email;
        if (string.IsNullOrWhiteSpace(rawRecipient))
            return Result.Failure<InvoiceReminderDto>(Error.NoRecipient());

        var email = EmailAddress.Create(rawRecipient);
        if (email.IsFailure) return Result.Failure<InvoiceReminderDto>(Error.InvalidRecipient());

        var sent = await _mailer.SendAsync(_tenant.TenantId, invoice, email.Value.Value, ReminderType.Manual, _tenant.Email, ct);
        return sent.IsSuccess ? Result.Success(ToDto(sent.Value)) : Result.Failure<InvoiceReminderDto>(sent.Error);
    }

    public async Task<Result<IReadOnlyList<InvoiceReminderDto>>> HistoryAsync(string invoiceId, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, invoiceId, ct);
        if (invoice is null) return Result.Failure<IReadOnlyList<InvoiceReminderDto>>(Error.NotFound());

        var items = await _reminders.ListByInvoiceAsync(_tenant.TenantId, invoiceId, ct);
        return Result.Success<IReadOnlyList<InvoiceReminderDto>>(items.Select(ToDto).ToList());
    }

    public async Task<ReminderSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var s = await _settings.GetAsync(_tenant.TenantId, ct);
        return new ReminderSettingsDto(s.AutoEnabled, s.DaysAfterDue);
    }

    public async Task<Result<ReminderSettingsDto>> UpdateSettingsAsync(ReminderSettingsDto dto, CancellationToken ct)
    {
        if (_tenant.Role is not (UserRole.Owner or UserRole.Admin))
            return Result.Failure<ReminderSettingsDto>(Error.Forbidden());
        if (dto.DaysAfterDue < 0)
            return Result.Failure<ReminderSettingsDto>(Error.Validation("Dagar efter förfall kan inte vara negativt."));

        await _settings.UpsertAsync(new ReminderSettings(_tenant.TenantId, dto.AutoEnabled, dto.DaysAfterDue), ct);
        return Result.Success(dto);
    }

    internal static InvoiceReminderDto ToDto(InvoiceReminder r) =>
        new(r.Id, r.InvoiceId, r.Type.ToString(), r.Recipient, r.Subject, r.Sequence, r.Status.ToString(), r.Error, r.SentAt);
}
