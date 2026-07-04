using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").RequireAuthorization();

        group.MapGet("", async (CustomerService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));

        group.MapPost("", async (CustomerRequest req, CustomerService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/customers/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}", async (string id, CustomerService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPut("/{id}", async (string id, CustomerRequest req, CustomerService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/archive", async (string id, CustomerService svc, CancellationToken ct) =>
        {
            var result = await svc.ArchiveAsync(id, ct);
            return result.IsSuccess ? Results.NoContent() : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
