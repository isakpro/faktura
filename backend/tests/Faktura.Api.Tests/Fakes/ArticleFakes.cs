using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Articles;

namespace Faktura.Api.Tests.Fakes;

public sealed class InMemoryArticleRepository : IArticleRepository
{
    private readonly ConcurrentDictionary<string, Article> _items = new();

    public Task AddAsync(Article article, CancellationToken ct = default)
    {
        _items[article.Id] = article;
        return Task.CompletedTask;
    }

    public Task<Article?> GetByIdAsync(string tenantId, string articleId, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(a => a.Id == articleId && a.TenantId == tenantId));

    public Task<IReadOnlyList<Article>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Article>>(_items.Values.Where(a => a.TenantId == tenantId).ToList());

    public Task UpdateAsync(Article article, CancellationToken ct = default)
    {
        _items[article.Id] = article;
        return Task.CompletedTask;
    }

    public Task<bool> SkuExistsAsync(string tenantId, string sku, string? excludeArticleId = null, CancellationToken ct = default)
        => Task.FromResult(_items.Values.Any(a =>
            a.TenantId == tenantId && a.Sku == sku && a.Id != excludeArticleId));
}
