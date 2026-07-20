using System.Text;
using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Accounting;
using Faktura.Domain.Common;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.Accounting;

/// <summary>SIE4-export av ett räkenskapsårs fakturor (spec 015): läses av Owner/Admin.</summary>
public static class SieExportEndpoints
{
    public static IEndpointRouteBuilder MapSieExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/export/sie", async (
            int year, ITenantContext tenant, IInvoiceRepository invoices, IOrganizationRepository organizations,
            IClock clock, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var all = await invoices.ListByTenantAsync(tenant.TenantId, ct);
            var org = await organizations.GetByIdAsync(tenant.TenantId, ct);
            var sie = SieExporter.Generate(all, org, year, clock.UtcNow);

            return Results.File(Encoding.Latin1.GetBytes(sie), "application/octet-stream", $"sie4-{year}.se");
        }).RequireAuthorization();

        return app;
    }
}
