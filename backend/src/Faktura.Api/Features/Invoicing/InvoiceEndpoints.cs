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

        // Body är valfri: utan radval krediteras hela fakturan (bakåtkompatibelt).
        group.MapPost("/{id}/credit", async (string id, CreditRequest? req, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.CreditAsync(id, req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/invoices/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/payments", async (string id, RegisterPaymentRequest req, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.RegisterPaymentAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}/payments", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.ListPaymentsAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        // Kundportalen (spec 013): skapa/återanvänd kundlänk.
        group.MapPost("/{id}/share", async (string id, InvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.ShareAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
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

        group.MapPost("/{id}/remind", async (string id, RemindRequest req, ReminderService svc, CancellationToken ct) =>
        {
            var result = await svc.RemindAsync(id, req.Recipient, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}/reminders", async (string id, ReminderService svc, CancellationToken ct) =>
        {
            var result = await svc.HistoryAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        app.MapGet("/api/dashboard", async (DashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(ct))).RequireAuthorization();

        // Kundportalen (spec 013): publika endpoints — token är behörigheten, ingen auth.
        var pub = app.MapGroup("/api/public/invoices");

        pub.MapGet("/{token}", async (string token, PublicInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(token, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        pub.MapGet("/{token}/pdf", async (string token, PublicInvoiceService svc, CancellationToken ct) =>
        {
            var result = await svc.PdfAsync(token, ct);
            return result.IsSuccess
                ? Results.File(result.Value.Bytes, "application/pdf", result.Value.FileName)
                : AuthEndpoints.ToProblem(result.Error);
        });

        var settings = app.MapGroup("/api/reminder-settings").RequireAuthorization();

        settings.MapGet("", async (ReminderService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetSettingsAsync(ct)));

        settings.MapPut("", async (ReminderSettingsDto dto, ReminderService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateSettingsAsync(dto, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
