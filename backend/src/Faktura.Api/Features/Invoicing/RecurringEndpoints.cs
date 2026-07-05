using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Invoicing;

public static class RecurringEndpoints
{
    public static IEndpointRouteBuilder MapRecurringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recurring-invoices").RequireAuthorization();

        group.MapGet("", async (RecurringInvoiceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));

        group.MapPost("", async (RecurringInvoiceRequest req, RecurringInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/recurring-invoices/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPut("/{id}", async (string id, RecurringInvoiceRequest req, RecurringInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/pause", async (string id, RecurringInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.PauseAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/resume", async (string id, RecurringInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.ResumeAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}/generated", async (string id, RecurringInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.GeneratedAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
