using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Kundportalen (spec 013): läser fakturor via portal-token, utan autentisering.
/// Token är kapabiliteten — svaret innehåller inga tenant- eller kund-id:n.
/// </summary>
public sealed class PublicInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IOrganizationRepository _organizations;
    private readonly IInvoicePdfGenerator _pdf;
    private readonly IClock _clock;

    public PublicInvoiceService(IInvoiceRepository invoices, IOrganizationRepository organizations,
        IInvoicePdfGenerator pdf, IClock clock)
    {
        _invoices = invoices;
        _organizations = organizations;
        _pdf = pdf;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.UtcNow);

    public async Task<Result<PublicInvoiceDto>> GetAsync(string token, CancellationToken ct)
    {
        var invoice = await FindAsync(token, ct);
        if (invoice is null) return Result.Failure<PublicInvoiceDto>(Error.NotFound());

        var org = await _organizations.GetByIdAsync(invoice.TenantId, ct);
        var profile = org?.Profile;
        var t = invoice.Totals;
        var status = invoice.IsOverdue(Today) ? "Overdue"
            : invoice.Status == InvoiceStatus.Sent && invoice.PaidAmount > 0 ? "PartiallyPaid"
            : invoice.Status.ToString();

        return Result.Success(new PublicInvoiceDto(
            invoice.Type.ToString(), status, invoice.Number,
            invoice.CustomerSnapshot?.Name ?? "",
            invoice.InvoiceDate, invoice.DueDate, invoice.OcrNumber,
            invoice.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPriceExclVat, (int)l.VatRate, l.Net.Amount, l.Vat.Amount, l.Unit)).ToList(),
            new InvoiceTotalsDto(t.Net.Amount, t.VatByRate.Select(v => new VatByRateDto(v.RatePercent, v.Vat.Amount)).ToList(), t.Gross.Amount),
            invoice.PaidAmount, invoice.RemainingAmount,
            new PublicSellerDto(org?.Name ?? "", profile?.OrgNumber, profile?.AddressLine, profile?.PostalCode,
                profile?.City, profile?.Bankgiro, profile?.Plusgiro, profile?.FSkatt ?? false)));
    }

    public async Task<Result<InvoiceService.InvoicePdf>> PdfAsync(string token, CancellationToken ct)
    {
        var invoice = await FindAsync(token, ct);
        if (invoice is null) return Result.Failure<InvoiceService.InvoicePdf>(Error.NotFound());

        var org = await _organizations.GetByIdAsync(invoice.TenantId, ct);
        return Result.Success(new InvoiceService.InvoicePdf(_pdf.Generate(invoice, org), $"faktura-{invoice.Number}.pdf"));
    }

    private Task<Invoice?> FindAsync(string token, CancellationToken ct)
        // Kortare värden än en riktig token (32 hex) slås aldrig upp — spar en DB-runda för skräp.
        => token.Length == 32 && token.All(char.IsAsciiLetterOrDigit)
            ? _invoices.GetByShareTokenAsync(token, ct)
            : Task.FromResult<Invoice?>(null);
}
