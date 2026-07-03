using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Members;

public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        // Authenticated, tenant-scoped operations.
        var authed = app.MapGroup("/api").RequireAuthorization();

        authed.MapGet("/members", async (MemberService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMembersAsync(ct)));

        authed.MapGet("/invitations", async (MemberService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListInvitationsAsync(ct)));

        authed.MapPost("/invitations", async (InviteRequest req, MemberService svc, CancellationToken ct) =>
        {
            var result = await svc.InviteAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/invitations/{result.Value.Invitation.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        authed.MapDelete("/invitations/{id}", async (string id, MemberService svc, CancellationToken ct) =>
        {
            var result = await svc.RevokeInvitationAsync(id, ct);
            return result.IsSuccess ? Results.NoContent() : AuthEndpoints.ToProblem(result.Error);
        });

        authed.MapPut("/members/{id}/role", async (string id, ChangeRoleRequest req, MemberService svc, CancellationToken ct) =>
        {
            var result = await svc.ChangeRoleAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        // Public: an invitee accepts before they have an account.
        app.MapPost("/api/invitations/{token}/accept", async (
            string token, AcceptInvitationRequest req, MemberService svc, CancellationToken ct) =>
        {
            var result = await svc.AcceptAsync(token, req, ct);
            return result.IsSuccess ? Results.Created("/api/me", result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
