using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.Members;

public sealed record AuditEntryDto(string ActorEmail, string Method, string Path, int StatusCode, DateTime OccurredAt);

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", async (ITenantContext tenant, IAuditLogRepository audit, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var entries = await audit.ListLatestAsync(tenant.TenantId, limit: 50, ct);
            return Results.Ok(entries.Select(e =>
                new AuditEntryDto(e.ActorEmail, e.Method, e.Path, e.StatusCode, e.OccurredAt)));
        }).RequireAuthorization();

        return app;
    }
}
