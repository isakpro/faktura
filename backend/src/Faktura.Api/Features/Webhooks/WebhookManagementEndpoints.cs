using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Users;
using Faktura.Domain.Webhooks;

namespace Faktura.Api.Features.Webhooks;

public sealed record CreateWebhookRequest(string Url);
public sealed record WebhookEndpointDto(string Id, string Url, DateTime CreatedAt);
public sealed record CreatedWebhookDto(string Id, string Url, string Secret);
public sealed record WebhookDeliveryDto(string Id, string EventType, bool Success, int? StatusCode, string? Error, DateTime CreatedAt);

/// <summary>Hantering av utgående webhook-mottagare (spec 016): Owner/Admin.</summary>
public static class WebhookManagementEndpoints
{
    public static IEndpointRouteBuilder MapWebhookManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks").RequireAuthorization();

        group.MapGet("", async (ITenantContext tenant, IWebhookEndpointRepository endpoints, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var all = await endpoints.ListByTenantAsync(tenant.TenantId, ct);
            return Results.Ok(all.Select(e => new WebhookEndpointDto(e.Id, e.Url, e.CreatedAt)));
        });

        group.MapPost("", async (CreateWebhookRequest req, ITenantContext tenant, IWebhookEndpointRepository endpoints,
            IIdGenerator ids, IClock clock, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());
            if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                return AuthEndpoints.ToProblem(Error.Validation("Ogiltig URL."));

            var endpoint = WebhookEndpoint.CreateNew(ids.NewId(), tenant.TenantId, req.Url.Trim(), clock.UtcNow);
            await endpoints.AddAsync(endpoint, ct);

            return Results.Created($"/api/webhooks/{endpoint.Id}", new CreatedWebhookDto(endpoint.Id, endpoint.Url, endpoint.Secret));
        });

        group.MapDelete("/{id}", async (string id, ITenantContext tenant, IWebhookEndpointRepository endpoints, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var endpoint = await endpoints.GetByIdAsync(tenant.TenantId, id, ct);
            if (endpoint is null) return AuthEndpoints.ToProblem(Error.NotFound());

            await endpoints.DeleteAsync(tenant.TenantId, id, ct);
            return Results.NoContent();
        });

        group.MapGet("/{id}/deliveries", async (string id, ITenantContext tenant,
            IWebhookEndpointRepository endpoints, IWebhookDeliveryRepository deliveries, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var endpoint = await endpoints.GetByIdAsync(tenant.TenantId, id, ct);
            if (endpoint is null) return AuthEndpoints.ToProblem(Error.NotFound());

            var log = await deliveries.ListByEndpointAsync(tenant.TenantId, id, ct);
            return Results.Ok(log.Select(d => new WebhookDeliveryDto(d.Id, d.EventType, d.Success, d.StatusCode, d.Error, d.CreatedAt)));
        });

        return app;
    }
}
