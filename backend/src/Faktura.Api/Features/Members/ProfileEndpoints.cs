using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.Members;

public sealed record InvoiceProfileDto(
    string? OrgNumber,
    string? AddressLine,
    string? PostalCode,
    string? City,
    string? Bankgiro,
    string? Plusgiro,
    bool FSkatt);

/// <summary>Organisationens fakturaprofil (spec 009): läses av alla, skrivs av Owner/Admin.</summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organization-profile").RequireAuthorization();

        group.MapGet("", async (ITenantContext tenant, IOrganizationRepository orgs, CancellationToken ct) =>
        {
            var org = await orgs.GetByIdAsync(tenant.TenantId, ct);
            var p = org?.Profile;
            return Results.Ok(new InvoiceProfileDto(
                p?.OrgNumber, p?.AddressLine, p?.PostalCode, p?.City, p?.Bankgiro, p?.Plusgiro, p?.FSkatt ?? false));
        });

        group.MapPut("", async (InvoiceProfileDto dto, ITenantContext tenant, IOrganizationRepository orgs, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var org = await orgs.GetByIdAsync(tenant.TenantId, ct);
            if (org is null) return AuthEndpoints.ToProblem(Error.NotFound());

            org.UpdateProfile(new InvoiceProfile(
                dto.OrgNumber, dto.AddressLine, dto.PostalCode, dto.City, dto.Bankgiro, dto.Plusgiro, dto.FSkatt));
            await orgs.UpdateAsync(org, ct);
            return Results.Ok(dto);
        });

        return app;
    }
}
