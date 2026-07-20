using Faktura.Api.Auth;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Domain.Common;
using Faktura.Domain.PublicApi;
using Microsoft.AspNetCore.Authorization;

namespace Faktura.Api.Features.PublicApi;

/// <summary>
/// Det publika, nyckel-autentiserade API:et (spec 016). Byggd ovanpå samma tjänster som
/// SPA:n använder — <see cref="ITenantContext"/> läser tenant/roll ur claims oavsett
/// autentiseringsschema, så InvoiceService/CustomerService fungerar oförändrade här.
/// Varje endpoint kräver ett specifikt scope på den använda API-nyckeln.
/// </summary>
public static class PublicApiEndpoints
{
    public static IEndpointRouteBuilder MapPublicApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser());

        group.MapGet("/invoices", async (string? status, HttpContext http, InvoiceService svc, CancellationToken ct) =>
        {
            if (!HasScope(http, ApiScopes.InvoicesRead)) return AuthEndpoints.ToProblem(Error.Forbidden());
            return Results.Ok(await svc.ListAsync(status, ct));
        });

        group.MapGet("/invoices/{id}", async (string id, HttpContext http, InvoiceService svc, CancellationToken ct) =>
        {
            if (!HasScope(http, ApiScopes.InvoicesRead)) return AuthEndpoints.ToProblem(Error.Forbidden());
            var result = await svc.GetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/customers", async (HttpContext http, CustomerService svc, CancellationToken ct) =>
        {
            if (!HasScope(http, ApiScopes.CustomersRead)) return AuthEndpoints.ToProblem(Error.Forbidden());
            return Results.Ok(await svc.ListAsync(ct));
        });

        group.MapGet("/customers/{id}", async (string id, HttpContext http, CustomerService svc, CancellationToken ct) =>
        {
            if (!HasScope(http, ApiScopes.CustomersRead)) return AuthEndpoints.ToProblem(Error.Forbidden());
            var result = await svc.GetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/customers", async (CustomerRequest req, HttpContext http, CustomerService svc, CancellationToken ct) =>
        {
            if (!HasScope(http, ApiScopes.CustomersWrite)) return AuthEndpoints.ToProblem(Error.Forbidden());
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/customers/{result.Value.Id}", result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }

    private static bool HasScope(HttpContext http, string scope) =>
        (http.User.FindFirst("scopes")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope);
}
