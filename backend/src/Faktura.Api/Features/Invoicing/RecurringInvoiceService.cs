using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Tenant-scoped hantering av återkommande fakturor (mallar).</summary>
public sealed class RecurringInvoiceService
{
    private readonly ITenantContext _tenant;
    private readonly IRecurringInvoiceRepository _recurring;
    private readonly IInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public RecurringInvoiceService(ITenantContext tenant, IRecurringInvoiceRepository recurring,
        IInvoiceRepository invoices, ICustomerRepository customers, IIdGenerator ids, IClock clock)
    {
        _tenant = tenant;
        _recurring = recurring;
        _invoices = invoices;
        _customers = customers;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RecurringInvoiceDto>> ListAsync(CancellationToken ct)
        => (await _recurring.ListByTenantAsync(_tenant.TenantId, ct)).Select(ToDto).ToList();

    public async Task<Result<RecurringInvoiceDto>> CreateAsync(RecurringInvoiceRequest req, CancellationToken ct)
    {
        var parsed = await ParseAsync(req, ct);
        if (parsed.IsFailure) return Result.Failure<RecurringInvoiceDto>(parsed.Error);
        var (interval, lines) = parsed.Value;

        var built = RecurringInvoice.CreateNew(_ids.NewId(), _tenant.TenantId, req.CustomerId,
            interval, req.StartDate, req.EndDate, lines, _clock.UtcNow);
        if (built.IsFailure) return Result.Failure<RecurringInvoiceDto>(built.Error);

        await _recurring.AddAsync(built.Value, ct);
        return Result.Success(ToDto(built.Value));
    }

    public async Task<Result<RecurringInvoiceDto>> UpdateAsync(string id, RecurringInvoiceRequest req, CancellationToken ct)
    {
        var recurring = await _recurring.GetByIdAsync(_tenant.TenantId, id, ct);
        if (recurring is null) return Result.Failure<RecurringInvoiceDto>(Error.NotFound());

        var parsed = await ParseAsync(req, ct);
        if (parsed.IsFailure) return Result.Failure<RecurringInvoiceDto>(parsed.Error);
        var (interval, lines) = parsed.Value;

        var updated = recurring.Update(req.CustomerId, interval, req.EndDate, lines, _clock.UtcNow);
        if (updated.IsFailure) return Result.Failure<RecurringInvoiceDto>(updated.Error);

        await _recurring.UpdateAsync(recurring, ct);
        return Result.Success(ToDto(recurring));
    }

    public Task<Result<RecurringInvoiceDto>> PauseAsync(string id, CancellationToken ct)
        => SetStatusAsync(id, pause: true, ct);

    public Task<Result<RecurringInvoiceDto>> ResumeAsync(string id, CancellationToken ct)
        => SetStatusAsync(id, pause: false, ct);

    public async Task<Result<IReadOnlyList<InvoiceListItemDto>>> GeneratedAsync(string id, CancellationToken ct)
    {
        var recurring = await _recurring.GetByIdAsync(_tenant.TenantId, id, ct);
        if (recurring is null) return Result.Failure<IReadOnlyList<InvoiceListItemDto>>(Error.NotFound());

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var invoices = (await _invoices.ListByTenantAsync(_tenant.TenantId, ct))
            .Where(i => i.RecurringSourceId == id)
            .OrderByDescending(i => i.Number)
            .Select(i => new InvoiceListItemDto(i.Id, i.Number,
                i.IsOverdue(today) ? "Overdue" : i.Status.ToString(), i.CustomerId, i.Totals.Gross.Amount, i.DueDate))
            .ToList();
        return Result.Success<IReadOnlyList<InvoiceListItemDto>>(invoices);
    }

    private async Task<Result<RecurringInvoiceDto>> SetStatusAsync(string id, bool pause, CancellationToken ct)
    {
        var recurring = await _recurring.GetByIdAsync(_tenant.TenantId, id, ct);
        if (recurring is null) return Result.Failure<RecurringInvoiceDto>(Error.NotFound());

        if (pause) recurring.Pause(_clock.UtcNow);
        else recurring.Resume(_clock.UtcNow);

        await _recurring.UpdateAsync(recurring, ct);
        return Result.Success(ToDto(recurring));
    }

    private async Task<Result<(RecurrenceInterval, List<InvoiceLine>)>> ParseAsync(RecurringInvoiceRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<RecurrenceInterval>(req.Interval, ignoreCase: true, out var interval))
            return Result.Failure<(RecurrenceInterval, List<InvoiceLine>)>(Error.Validation("Ogiltigt intervall (monthly/quarterly/yearly)."));

        var customer = await _customers.GetByIdAsync(_tenant.TenantId, req.CustomerId, ct);
        if (customer is null)
            return Result.Failure<(RecurrenceInterval, List<InvoiceLine>)>(Error.Validation("Okänd kund."));

        var lines = new List<InvoiceLine>();
        foreach (var l in req.Lines)
        {
            if (!VatRateExtensions.IsValid(l.VatRate))
                return Result.Failure<(RecurrenceInterval, List<InvoiceLine>)>(Error.Validation($"Ogiltig momssats: {l.VatRate}."));
            lines.Add(new InvoiceLine(l.Description, l.Quantity, l.UnitPriceExclVat, VatRateExtensions.FromPercent(l.VatRate), l.Unit));
        }
        return Result.Success((interval, lines));
    }

    private static RecurringInvoiceDto ToDto(RecurringInvoice r) => new(
        r.Id, r.CustomerId, r.Interval.ToString(), r.Status.ToString(),
        r.StartDate, r.NextRunDate, r.EndDate,
        r.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPriceExclVat, (int)l.VatRate, l.Net.Amount, l.Vat.Amount, l.Unit)).ToList(),
        r.Totals.Gross.Amount);
}
