using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

/// <summary>A persisted document owned by a tenant.</summary>
internal interface ITenantDocument
{
    string Id { get; }
    string TenantId { get; }
}

/// <summary>
/// Base for repositories over tenant-owned collections. Every read/write goes through a
/// filter that includes <c>tenantId</c>, so a query can never reach another tenant's data
/// by construction (constitution V / FR-007). Callers pass the tenant id derived from the
/// authenticated context.
/// </summary>
internal abstract class TenantScopedRepository<TDoc> where TDoc : ITenantDocument
{
    protected readonly IMongoCollection<TDoc> Collection;

    protected TenantScopedRepository(IMongoCollection<TDoc> collection) => Collection = collection;

    private static FilterDefinition<TDoc> TenantFilter(string tenantId) =>
        Builders<TDoc>.Filter.Eq(d => d.TenantId, tenantId);

    private static FilterDefinition<TDoc> ById(string tenantId, string id) =>
        Builders<TDoc>.Filter.And(TenantFilter(tenantId), Builders<TDoc>.Filter.Eq(d => d.Id, id));

    protected Task<List<TDoc>> ListAsync(string tenantId, CancellationToken ct) =>
        Collection.Find(TenantFilter(tenantId)).ToListAsync(ct);

    protected async Task<TDoc?> FindByIdAsync(string tenantId, string id, CancellationToken ct) =>
        await Collection.Find(ById(tenantId, id)).FirstOrDefaultAsync(ct);

    protected Task InsertAsync(TDoc document, CancellationToken ct) =>
        Collection.InsertOneAsync(document, cancellationToken: ct);

    protected Task ReplaceAsync(string tenantId, string id, TDoc document, CancellationToken ct) =>
        Collection.ReplaceOneAsync(ById(tenantId, id), document, cancellationToken: ct);

    protected Task<long> CountAsync(string tenantId, FilterDefinition<TDoc> extra, CancellationToken ct) =>
        Collection.CountDocumentsAsync(Builders<TDoc>.Filter.And(TenantFilter(tenantId), extra), cancellationToken: ct);
}
