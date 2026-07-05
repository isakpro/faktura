using Faktura.Api.Features.Auth;

namespace Faktura.Api.Features.Articles;

public static class ArticleEndpoints
{
    public static IEndpointRouteBuilder MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/articles").RequireAuthorization();

        group.MapGet("", async (string? status, ArticleService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(status, ct)));

        group.MapPost("", async (ArticleRequest req, ArticleService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(req, ct);
            return result.IsSuccess
                ? Results.Created($"/api/articles/{result.Value.Id}", result.Value)
                : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapGet("/{id}", async (string id, ArticleService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAsync(id, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPut("/{id}", async (string id, ArticleRequest req, ArticleService svc, CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : AuthEndpoints.ToProblem(result.Error);
        });

        group.MapPost("/{id}/archive", async (string id, ArticleService svc, CancellationToken ct) =>
        {
            var result = await svc.ArchiveAsync(id, ct);
            return result.IsSuccess ? Results.NoContent() : AuthEndpoints.ToProblem(result.Error);
        });

        return app;
    }
}
