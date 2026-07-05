namespace Faktura.Api.Features.Invoicing;

public sealed record MonthlyRevenueDto(int Year, int Month, decimal Gross);

public sealed record DashboardDto(
    decimal Outstanding,
    decimal Overdue,
    decimal PaidThisYear,
    IReadOnlyList<MonthlyRevenueDto> MonthlyRevenue,
    IReadOnlyList<InvoiceListItemDto> RecentInvoices);
