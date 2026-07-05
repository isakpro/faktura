using Faktura.Domain.Articles;

namespace Faktura.Domain.Abstractions;

public interface IArticleRepository
{
    Task AddAsync(Article article, CancellationToken ct = default);
    Task<Article?> GetByIdAsync(string tenantId, string articleId, CancellationToken ct = default);
    Task<IReadOnlyList<Article>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task UpdateAsync(Article article, CancellationToken ct = default);

    /// <summary>True om SKU:t redan används av en annan artikel i organisationen.</summary>
    Task<bool> SkuExistsAsync(string tenantId, string sku, string? excludeArticleId = null, CancellationToken ct = default);
}
