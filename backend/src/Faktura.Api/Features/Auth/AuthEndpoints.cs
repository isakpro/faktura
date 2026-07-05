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

            return Results.Ok(new MeResponse(user.ToDto(), org.ToDto()));
        }).RequireAuthorization();

        return app;
    }

    /// <summary>Maps a domain <see cref="Error"/> to an RFC 7807 problem response.</summary>
    internal static IResult ToProblem(Error error) => error.Code switch
    {
        "weak_password" => Results.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Svagt lösenord"),
        "email_in_use" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "E-post upptagen"),
        "invalid_credentials" => Results.Problem(error.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Ogiltiga uppgifter"),
        "too_many_attempts" => new RetryAfterProblem(error.RetryAfterSeconds ?? 60, error.Message),
        "forbidden" => Results.Problem(error.Message, statusCode: StatusCodes.Status403Forbidden, title: "Otillåtet"),
        "not_found" => Results.Problem(error.Message, statusCode: StatusCodes.Status404NotFound, title: "Hittades inte"),
        "seat_limit" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Platsgräns nådd"),
        "last_owner" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Sista Owner"),
        "invitation_invalid" => Results.Problem(error.Message, statusCode: StatusCodes.Status410Gone, title: "Ogiltig inbjudan"),
        "invoice_locked" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Fakturan är låst"),
        "empty_invoice" => Results.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Tom faktura"),
        "invalid_state" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Ogiltig status"),
        "over_credit" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Överkreditering"),
        "no_recipient" => Results.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Mottagare saknas"),
        "invalid_recipient" => Results.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Ogiltig mottagare"),
        "email_failed" => Results.Problem(error.Message, statusCode: StatusCodes.Status502BadGateway, title: "E-post misslyckades"),
        "sku_taken" => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Artikelnummer upptaget"),
        _ => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest, title: "Valideringsfel")
    };
}

/// <summary>A 429 problem response that also sets the <c>Retry-After</c> header.</summary>
internal sealed class RetryAfterProblem(int retryAfterSeconds, string message) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        await Results.Problem(message, statusCode: StatusCodes.Status429TooManyRequests, title: "För många försök")
            .ExecuteAsync(httpContext);
    }
}
