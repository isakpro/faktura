using Faktura.Domain.Abstractions;
using Faktura.Domain.Articles;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoArticleRepository : TenantScopedRepository<ArticleDocument>, IArticleRepository
{
    public MongoArticleRepository(MongoContext context) : base(context.Articles) { }

    public Task AddAsync(Article article, CancellationToken ct = default)
        => InsertAsync(ArticleDocument.FromDomain(article), ct);

    public async Task<Article?> GetByIdAsync(string tenantId, string articleId, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, articleId, ct))?.ToDomain();

    public async Task<IReadOnlyList<Article>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public Task UpdateAsync(Article article, CancellationToken ct = default)
        => ReplaceAsync(article.TenantId, article.Id, ArticleDocument.FromDomain(article), ct);

    public async Task<bool> SkuExistsAsync(string tenantId, string sku, string? excludeArticleId = null, CancellationToken ct = default)
    {
        var filter = Builders<ArticleDocument>.Filter.Eq(a => a.Sku, sku);
        if (excludeArticleId is not null)
            filter &= Builders<ArticleDocument>.Filter.Ne(a => a.Id, excludeArticleId);
        return await CountAsync(tenantId, filter, ct) > 0;
    }
}
