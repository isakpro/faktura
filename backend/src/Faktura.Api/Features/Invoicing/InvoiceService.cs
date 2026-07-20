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
    private readonly IInvoicePdfGenerator _pdf;
    private readonly IOrganizationRepository _organizations;
    private readonly IInvoicePaymentRepository _payments;
    private readonly PortalLinks _portal;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public InvoiceService(ITenantContext tenant, IInvoiceRepository invoices, ICustomerRepository customers,
        IInvoiceNumberSequence numbers, IInvoicePdfGenerator pdf, IOrganizationRepository organizations,
        IInvoicePaymentRepository payments, PortalLinks portal, IIdGenerator ids, IClock clock)
    {
        _portal = portal;
        _tenant = tenant;
        _invoices = invoices;
        _customers = customers;
        _numbers = numbers;
        _pdf = pdf;
        _organizations = organizations;
        _payments = payments;
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

    /// <summary>"Betald"-knappen: socker för att betala hela kvarvarande saldot — via reskontran.</summary>
    public Task<Result<InvoiceDto>> MarkPaidAsync(string id, DateOnly paidDate, CancellationToken ct)
        => WithInvoiceAsync(id, ct, invoice =>
            RegisterPaymentInternalAsync(invoice, invoice.RemainingAmount, paidDate, note: null, ct));

    public Task<Result<InvoiceDto>> RegisterPaymentAsync(string id, RegisterPaymentRequest req, CancellationToken ct)
        => WithInvoiceAsync(id, ct, invoice =>
            RegisterPaymentInternalAsync(invoice, req.Amount, req.PaidDate ?? Today, req.Note, ct));

    private async Task<Result<InvoiceDto>> RegisterPaymentInternalAsync(
        Invoice invoice, decimal amount, DateOnly paidDate, string? note, CancellationToken ct)
    {
        var result = invoice.RegisterPayment(amount, paidDate, _clock.UtcNow);
        if (result.IsFailure) return Result.Failure<InvoiceDto>(result.Error);

        await _payments.AddAsync(
            new InvoicePayment(_ids.NewId(), _tenant.TenantId, invoice.Id, amount, paidDate, note, _clock.UtcNow), ct);
        await _invoices.UpdateAsync(invoice, ct);
        return Result.Success(ToDto(invoice));
    }

    public async Task<Result<IReadOnlyList<PaymentDto>>> ListPaymentsAsync(string id, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<IReadOnlyList<PaymentDto>>(Error.NotFound());

        var payments = await _payments.ListByInvoiceAsync(_tenant.TenantId, id, ct);
        return Result.Success<IReadOnlyList<PaymentDto>>(
            payments.Select(p => new PaymentDto(p.Id, p.Amount, p.PaidDate, p.Note, p.CreatedAt)).ToList());
    }

    public async Task<Result<InvoiceDto>> CreditAsync(string id, CreditRequest? req, CancellationToken ct)
    {
        var original = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (original is null) return Result.Failure<InvoiceDto>(Error.NotFound());

        // Validera radval INNAN ett nummer förbrukas, så serien inte får hopp vid nekad kreditering.
        var selections = req?.Lines?.Select(l => new CreditSelection(l.Index, l.Quantity)).ToList();
        var creditLines = original.BuildCreditLines(selections);
        if (creditLines.IsFailure) return Result.Failure<InvoiceDto>(creditLines.Error);

        var number = await _numbers.NextAsync(_tenant.TenantId, ct);
        var creditNote = Invoice.CreateCreditNote(_ids.NewId(), original, number, Today, _clock.UtcNow, creditLines.Value);
        original.RegisterCredit(-creditNote.Totals.Gross.Amount, _clock.UtcNow);

        await _invoices.AddAsync(creditNote, ct);
        await _invoices.UpdateAsync(original, ct);
        return Result.Success(ToDto(creditNote));
    }

    /// <summary>Kundlänk (spec 013): tilldelar portal-token vid behov och returnerar den stabila länken.</summary>
    public async Task<Result<ShareLinkDto>> ShareAsync(string id, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<ShareLinkDto>(Error.NotFound());

        var url = await _portal.EnsureAsync(invoice, ct);
        return url is null
            ? Result.Failure<ShareLinkDto>(Error.InvalidState())
            : Result.Success(new ShareLinkDto(url, invoice.ShareToken!));
    }

    private async Task<Result<InvoiceDto>> WithInvoiceAsync(
        string id, CancellationToken ct, Func<Invoice, Task<Result<InvoiceDto>>> action)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        return invoice is null ? Result.Failure<InvoiceDto>(Error.NotFound()) : await action(invoice);
    }

    public sealed record InvoicePdf(byte[] Bytes, string FileName);

    public async Task<Result<InvoicePdf>> GeneratePdfAsync(string id, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(_tenant.TenantId, id, ct);
        if (invoice is null) return Result.Failure<InvoicePdf>(Error.NotFound());
        if (invoice.Number is null) return Result.Failure<InvoicePdf>(Error.InvalidState()); // utkast har ingen PDF

        var org = await _organizations.GetByIdAsync(_tenant.TenantId, ct);
        var bytes = _pdf.Generate(invoice, org);
        var prefix = invoice.Type == InvoiceType.CreditNote ? "kreditfaktura" : "faktura";
        return Result.Success(new InvoicePdf(bytes, $"{prefix}-{invoice.Number}.pdf"));
    }

    private static Result<List<InvoiceLine>> ToLines(IEnumerable<InvoiceLineInput> inputs)
    {
        var lines = new List<InvoiceLine>();
        foreach (var l in inputs)
        {
            if (!VatRateExtensions.IsValid(l.VatRate))
                return Result.Failure<List<InvoiceLine>>(Error.Validation($"Ogiltig momssats: {l.VatRate}."));
            lines.Add(new InvoiceLine(l.Description, l.Quantity, l.UnitPriceExclVat, VatRateExtensions.FromPercent(l.VatRate), l.Unit));
        }
        return Result.Success(lines);
    }

    private string EffectiveStatus(Invoice i) =>
        i.IsOverdue(Today) ? "Overdue"
        : i.Status == InvoiceStatus.Sent && i.PaidAmount > 0 ? "PartiallyPaid"
        : i.Status.ToString();

    private InvoiceDto ToDto(Invoice i)
    {
        var t = i.Totals;
        return new InvoiceDto(
            i.Id, i.Type.ToString(), EffectiveStatus(i), i.Number, i.CustomerId,
            i.InvoiceDate, i.DueDate, i.PaidDate, i.OriginalInvoiceId,
            i.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPriceExclVat, (int)l.VatRate, l.Net.Amount, l.Vat.Amount, l.Unit)).ToList(),
            new InvoiceTotalsDto(t.Net.Amount, t.VatByRate.Select(v => new VatByRateDto(v.RatePercent, v.Vat.Amount)).ToList(), t.Gross.Amount),
            i.OcrNumber, i.PaidAmount, i.RemainingAmount);
    }

    private InvoiceListItemDto ToListItem(Invoice i) =>
        new(i.Id, i.Number, EffectiveStatus(i), i.CustomerId, i.Totals.Gross.Amount, i.DueDate);
}
