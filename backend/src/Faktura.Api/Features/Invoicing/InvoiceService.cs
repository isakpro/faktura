using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Customers;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Tenant-scoped hantering av fakturor: utkast, skick (nummer/lås), betalstatus.</summary>
public sealed class InvoiceService
{
    private readonly ITenantContext _tenant;
    private readonly IInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IInvoiceNumberSequence _numbers;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public InvoiceService(ITenantContext tenant, IInvoiceRepository invoices, ICustomerRepository customers,
        IInvoiceNumberSequence numbers, IIdGenerator ids, IClock clock)
    {
        _tenant = tenant;
        _invoices = invoices;
        _customers = customers;
        _numbers = numbers;
        _ids = ids;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.UtcNow);

    public async Task<Result<InvoiceDto>> CreateDraftAsync(CreateInvoiceRequest req, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(_tenant.TenantId, req.CustomerId, ct);
        if (customer is null) return Result.Failure<InvoiceDto>(Error.Validation("Okänd kund."));

        var lines = ToLines(req.Lines);
        if (lines.IsFailure) return Result.Failure<InvoiceDto>(lines.Error);

        var invoice = Invoice.CreateDraft(_ids.NewId(), _tenant.TenantId, req.CustomerId, lines.Value, _clock.UtcNow);
        await _invoices.AddAsync(invoice, ct);
        return Result.Success(ToDto(invoice));
    }

    public async Task<Result<InvoiceDto>> GetAsync(string id, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        return invoice is null ? Result.Failure<InvoiceDto>(Error.NotFound()) : Result.Success(ToDto(invoice));
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(string? status, CancellationToken ct)
    {
        var all = await _invoices.ListByTenantAsync(_tenant.TenantId, ct);
        IEnumerable<Invoice> filtered = status?.ToLowerInvariant() switch
        {
            "draft" => all.Where(i => i.Status == InvoiceStatus.Draft),
            "sent" => all.Where(i => i.Status == InvoiceStatus.Sent),
            "paid" => all.Where(i => i.Status == InvoiceStatus.Paid),
            "credited" => all.Where(i => i.Status == InvoiceStatus.Credited),
            "overdue" => all.Where(i => i.IsOverdue(Today)),
            _ => all
        };
        return filtered.Select(ToListItem).ToList();
    }

    public async Task<Result<InvoiceDto>> UpdateDraftAsync(string id, UpdateInvoiceRequest req, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<InvoiceDto>(Error.NotFound());

        var lines = ToLines(req.Lines);
        if (lines.IsFailure) return Result.Failure<InvoiceDto>(lines.Error);

        var customer = await _customers.GetByIdAsync(_tenant.TenantId, req.CustomerId, ct);
        if (customer is null) return Result.Failure<InvoiceDto>(Error.Validation("Okänd kund."));

        var changeCustomer = invoice.ChangeCustomer(req.CustomerId, _clock.UtcNow);
        if (changeCustomer.IsFailure) return Result.Failure<InvoiceDto>(changeCustomer.Error);
        var replace = invoice.ReplaceLines(lines.Value, _clock.UtcNow);
        if (replace.IsFailure) return Result.Failure<InvoiceDto>(replace.Error);

        await _invoices.UpdateAsync(invoice, ct);
        return Result.Success(ToDto(invoice));
    }

    public async Task<Result<InvoiceDto>> SendAsync(string id, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<InvoiceDto>(Error.NotFound());

        var customer = await _customers.GetByIdAsync(_tenant.TenantId, invoice.CustomerId, ct);
        if (customer is null) return Result.Failure<InvoiceDto>(Error.Validation("Okänd kund."));

        // Atomiskt nästa nummer per tenant.
        var number = await _numbers.NextAsync(_tenant.TenantId, ct);
        var snapshot = new CustomerSnapshot(customer.Name, customer.Email, customer.OrgNumber, customer.VatNumber, customer.Address);

        var result = invoice.Send(number, Today, snapshot, customer.PaymentTermsDays, _clock.UtcNow);
        if (result.IsFailure) return Result.Failure<InvoiceDto>(result.Error);

        await _invoices.UpdateAsync(invoice, ct);
        return Result.Success(ToDto(invoice));
    }

    public async Task<Result<InvoiceDto>> MarkPaidAsync(string id, DateOnly paidDate, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<InvoiceDto>(Error.NotFound());

        var result = invoice.MarkPaid(paidDate, _clock.UtcNow);
        if (result.IsFailure) return Result.Failure<InvoiceDto>(result.Error);

        await _invoices.UpdateAsync(invoice, ct);
        return Result.Success(ToDto(invoice));
    }

    private static Result<List<InvoiceLine>> ToLines(IEnumerable<InvoiceLineInput> inputs)
    {
        var lines = new List<InvoiceLine>();
        foreach (var l in inputs)
        {
            if (!VatRateExtensions.IsValid(l.VatRate))
                return Result.Failure<List<InvoiceLine>>(Error.Validation($"Ogiltig momssats: {l.VatRate}."));
            lines.Add(new InvoiceLine(l.Description, l.Quantity, l.UnitPriceExclVat, VatRateExtensions.FromPercent(l.VatRate)));
        }
        return Result.Success(lines);
    }

    private string EffectiveStatus(Invoice i) => i.IsOverdue(Today) ? "Overdue" : i.Status.ToString();

    private InvoiceDto ToDto(Invoice i)
    {
        var t = i.Totals;
        return new InvoiceDto(
            i.Id, i.Type.ToString(), EffectiveStatus(i), i.Number, i.CustomerId,
            i.InvoiceDate, i.DueDate, i.PaidDate, i.OriginalInvoiceId,
            i.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPriceExclVat, (int)l.VatRate, l.Net.Amount, l.Vat.Amount)).ToList(),
            new InvoiceTotalsDto(t.Net.Amount, t.VatByRate.Select(v => new VatByRateDto(v.RatePercent, v.Vat.Amount)).ToList(), t.Gross.Amount));
    }

    private InvoiceListItemDto ToListItem(Invoice i) =>
        new(i.Id, i.Number, EffectiveStatus(i), i.CustomerId, i.Totals.Gross.Amount, i.DueDate);
}
