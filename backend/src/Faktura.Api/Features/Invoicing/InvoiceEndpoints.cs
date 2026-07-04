using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Invoicing;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices").RequireAuthorization();

        group.MapGet("", async (string? status, InvoiceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(status, ct)));

        group.MapPost("", async (CreateInvoiceRequest req, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateDraftAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/invoices/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPut("/{id}", async (string id, UpdateInvoiceRequest req, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateDraftAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/send", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.SendAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/mark-paid", async (string id, MarkPaidRequest req, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.MarkPaidAsync(id, req.PaidDate, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/credit", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.CreditAsync(id, ct);
            return result.IsSuccess
                ? Results.Created($"/api/invoices/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}/pdf", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.GeneratePdfAsync(id, ct);
            return result.IsSuccess
                ? Results.File(result.Value.Bytes, "application/pdf", result.Value.FileName)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/email", async (string id, SendEmailRequest req, EmailService svc, CancellationToken ct) =>
        {
            var result = await svc.SendAsync(id, req.Recipient, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}/emails", async (string id, EmailService svc, CancellationToken ct) =>
        {
            var result = await svc.HistoryAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
