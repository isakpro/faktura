using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Features.Invoicing;

/// <summary>Översiktens nyckeltal — ren läsvy, tenant-isolerad, alla roller.</summary>
public sealed class DashboardService
{
    private readonly ITenantContext _tenant;
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public DashboardService(ITenantContext tenant, IInvoiceRepository invoices, IClock clock)
    {
        _tenant = tenant;
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var invoices = await _invoices.ListByTenantAsync(_tenant.TenantId, ct);

        var figures = DashboardCalculator.Compute(invoices, today);

        var recent = invoices
            .OrderByDescending(i => i.UpdatedAt)
            .Take(5)
            .Select(i => new InvoiceListItemDto(
                i.Id, i.Number, i.IsOverdue(today) ? "Overdue" : i.Status.ToString(),
                i.CustomerId, i.Totals.Gross.Amount, i.DueDate))
            .ToList();

        return new DashboardDto(
            figures.Outstanding,
            figures.Overdue,
            figures.PaidThisYear,
            figures.MonthlyRevenue.Select(m => new MonthlyRevenueDto(m.Year, m.Month, m.Gross)).ToList(),
            recent);
    }
}
