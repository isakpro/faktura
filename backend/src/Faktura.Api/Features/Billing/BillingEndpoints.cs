using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Billing;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var authed = app.MapGroup("/api/billing").RequireAuthorization();

        authed.MapGet("", async (BillingService svc, CancellationToken ct) =>
        {
            var result = await svc.GetBillingAsync(ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        authed.MapPost("/checkout", async (CheckoutRequest req, BillingService svc, CancellationToken ct) =>
        {
            var result = await svc.CheckoutAsync(req.ReturnUrl, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        // Public webhook: authenticity comes from the Stripe signature, not a JWT.
        app.MapPost("/api/billing/webhook", async (HttpRequest request, BillingService svc, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["Stripe-Signature"].ToString();

            var result = await svc.HandleWebhookAsync(payload, signature, ct);
            return result.IsSuccess ? Results.Ok() : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
