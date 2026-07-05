using Faktura.Domain.Abstractions;
using Faktura.Domain.Articles;
using Faktura.Domain.Common;

namespace Faktura.Api.Features.Articles;

/// <summary>Tenant-scoped hantering av artikelregistret. Alla roller (FR-005).</summary>
public sealed class ArticleService
{
    private readonly ITenantContext _tenant;
    private readonly IArticleRepository _articles;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public ArticleService(ITenantContext tenant, IArticleRepository articles, IIdGenerator ids, IClock clock)
    {
        _tenant = tenant;
        _articles = articles;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ArticleDto>> ListAsync(string? status, CancellationToken ct)
    {
        var all = await _articles.ListByTenantAsync(_tenant.TenantId, ct);
        IEnumerable<Article> filtered = status?.ToLowerInvariant() switch
        {
            "archived" => all.Where(a => a.Status == ArticleStatus.Archived),
            "all" => all,
            _ => all.Where(a => a.Status == ArticleStatus.Active), // default: aktiva (väljaren)
        };
        return filtered.OrderBy(a => a.Name).Select(ToDto).ToList();
    }

    public async Task<Result<ArticleDto>> GetAsync(string id, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(_tenant.TenantId, id, ct);
        return article is null ? Result.Failure<ArticleDto>(Error.NotFound()) : Result.Success(ToDto(article));
    }

    public async Task<Result<ArticleDto>> CreateAsync(ArticleRequest req, CancellationToken ct)
    {
        var built = Article.CreateNew(_ids.NewId(), _tenant.TenantId, req.Name, req.Sku, req.Unit,
            req.UnitPriceExclVat, req.VatRate, _clock.UtcNow);
        if (built.IsFailure) return Result.Failure<ArticleDto>(built.Error);

        if (built.Value.Sku is { } sku && await _articles.SkuExistsAsync(_tenant.TenantId, sku, ct: ct))
            return Result.Failure<ArticleDto>(Error.SkuTaken());

        await _articles.AddAsync(built.Value, ct);
        return Result.Success(ToDto(built.Value));
    }

    public async Task<Result<ArticleDto>> UpdateAsync(string id, ArticleRequest req, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(_tenant.TenantId, id, ct);
        if (article is null) return Result.Failure<ArticleDto>(Error.NotFound());

        var updated = article.Update(req.Name, req.Sku, req.Unit, req.UnitPriceExclVat, req.VatRate);
        if (updated.IsFailure) return Result.Failure<ArticleDto>(updated.Error);

        if (article.Sku is { } sku && await _articles.SkuExistsAsync(_tenant.TenantId, sku, excludeArticleId: id, ct))
            return Result.Failure<ArticleDto>(Error.SkuTaken());

        await _articles.UpdateAsync(article, ct);
        return Result.Success(ToDto(article));
    }

    public async Task<Result> ArchiveAsync(string id, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(_tenant.TenantId, id, ct);
        if (article is null) return Result.Failure(Error.NotFound());
        article.Archive();
        await _articles.UpdateAsync(article, ct);
        return Result.Success();
    }

    private static ArticleDto ToDto(Article a) =>
        new(a.Id, a.Name, a.Sku, a.Unit, a.UnitPriceExclVat, (int)a.VatRate, a.Status.ToString());
}
