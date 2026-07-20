using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Delad hjälpare för kundportalen (spec 013): säkerställer att fakturan har en portal-token
/// och bygger den publika länken. Tenant-explicit via fakturan — fungerar även i systemkontext
/// (mailer-jobben).
/// </summary>
public sealed class PortalLinks
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;
    private readonly AppOptions _app;

    public PortalLinks(IInvoiceRepository invoices, IClock clock, IOptions<AppOptions> app)
    {
        _invoices = invoices;
        _clock = clock;
        _app = app.Value;
    }

    public string BuildUrl(string token) => $"{_app.BaseUrl.TrimEnd('/')}/f/{token}";

    /// <summary>Portallänken för fakturan; tilldelar token vid behov. Null om fakturan inte kan delas.</summary>
    public async Task<string?> EnsureAsync(Invoice invoice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_app.BaseUrl)) return null;
        if (invoice.ShareToken is null)
        {
            if (invoice.AssignShareToken(ShareTokens.New(), _clock.UtcNow).IsFailure) return null;
            await _invoices.UpdateAsync(invoice, ct);
        }
        return BuildUrl(invoice.ShareToken!);
    }
}
