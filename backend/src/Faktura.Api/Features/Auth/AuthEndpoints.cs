using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;

namespace Faktura.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (RegisterRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/organizations/{result.Value.Organization.Id}", result.Value)
                : ToProblem(result.Error);
        });

        auth.MapPost("/login", async (LoginRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.LoginAsync(req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);
        });

        auth.MapPost("/refresh", async (RefreshRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RefreshAsync(req.RefreshToken, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);
        });

        auth.MapPost("/logout", async (RefreshRequest req, AuthService svc, CancellationToken ct) =>
        {
            await svc.LogoutAsync(req.RefreshToken, ct);
            return Results.NoContent();
        });

        app.MapGet("/api/me", async (
            ITenantContext tenant,
            IUserRepository users,
            IOrganizationRepository organizations,
            CancellationToken ct) =>
        {
            var user = await users.GetByIdAsync(tenant.TenantId, tenant.UserId, ct);
            var org = await organizations.GetByIdAsync(tenant.TenantId, ct);
            if (user is null || org is null)
                return Results.Problem("Kontot kunde inte hittas.", statusCode: StatusCodes.Status404NotFound);

            return Results.Ok(new MeResponse(AuthService.Map(user), AuthService.Map(org)));
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Maps a domain <see cref="Error"/> to an RFC 7807 problem response.</summary>
    internal static IResult ToProblem(Error error) => error.Code switch
    {
        "weak_password" => Results.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Svagt lösenord"),
        "email_in_use" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "E-post upptagen"),
        "invalid_credentials" => Results.Problem(error.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Ogiltiga uppgifter"),
        _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest, title: "Valideringsfel")
    };
}
