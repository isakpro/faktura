using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.PublicApi;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.ApiKeys;

public sealed record CreateApiKeyRequest(string Name, List<string> Scopes);
public sealed record ApiKeyDto(string Id, string Name, string Prefix, IReadOnlyList<string> Scopes, DateTime CreatedAt, DateTime? LastUsedAt);
public sealed record CreatedApiKeyDto(string Id, string Name, string Key, IReadOnlyList<string> Scopes);

/// <summary>Hantering av API-nycklar för det publika API:et (spec 016): Owner/Admin.</summary>
public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/api-keys").RequireAuthorization();

        group.MapGet("", async (ITenantContext tenant, IApiKeyRepository keys, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var all = await keys.ListByTenantAsync(tenant.TenantId, ct);
            return Results.Ok(all.Select(k => new ApiKeyDto(k.Id, k.Name, k.Prefix, k.Scopes, k.CreatedAt, k.LastUsedAt)));
        });

        group.MapPost("", async (CreateApiKeyRequest req, ITenantContext tenant, IApiKeyRepository keys,
            IIdGenerator ids, IClock clock, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());
            if (string.IsNullOrWhiteSpace(req.Name))
                return AuthEndpoints.ToProblem(Error.Validation("Namn krävs."));
            var scopes = req.Scopes.Where(ApiScopes.All.Contains).Distinct().ToList();
            if (scopes.Count == 0)
                return AuthEndpoints.ToProblem(Error.Validation("Minst ett giltigt scope krävs."));

            var (raw, _) = ApiKeyGenerator.New();
            var key = ApiKey.CreateNew(ids.NewId(), tenant.TenantId, req.Name.Trim(), raw, scopes, clock.UtcNow);
            await keys.AddAsync(key, ct);

            return Results.Created($"/api/api-keys/{key.Id}", new CreatedApiKeyDto(key.Id, key.Name, raw, key.Scopes));
        });

        group.MapDelete("/{id}", async (string id, ITenantContext tenant, IApiKeyRepository keys, IClock clock, CancellationToken ct) =>
        {
            if (tenant.Role is not (UserRole.Owner or UserRole.Admin))
                return AuthEndpoints.ToProblem(Error.Forbidden());

            var key = await keys.GetByIdAsync(tenant.TenantId, id, ct);
            if (key is null) return AuthEndpoints.ToProblem(Error.NotFound());

            key.Revoke(clock.UtcNow);
            await keys.UpdateAsync(key, ct);
            return Results.NoContent();
        });

        return app;
    }
}
