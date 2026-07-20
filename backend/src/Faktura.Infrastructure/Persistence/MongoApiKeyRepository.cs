using Faktura.Domain.Abstractions;
using Faktura.Domain.PublicApi;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoApiKeyRepository : TenantScopedRepository<ApiKeyDocument>, IApiKeyRepository
{
    public MongoApiKeyRepository(MongoContext context) : base(context.ApiKeys) { }

    public Task AddAsync(ApiKey key, CancellationToken ct = default)
        => InsertAsync(ApiKeyDocument.FromDomain(key), ct);

    public async Task<IReadOnlyList<ApiKey>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public async Task<ApiKey?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, id, ct))?.ToDomain();

    public Task UpdateAsync(ApiKey key, CancellationToken ct = default)
        => ReplaceAsync(key.TenantId, key.Id, ApiKeyDocument.FromDomain(key), ct);

    public async Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
    {
        // Systemkontext (spec 016): medvetet utanför tenant-filtret — hashen är sökvägen in.
        var doc = await Collection.Find(Builders<ApiKeyDocument>.Filter.Eq(d => d.KeyHash, keyHash)).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }
}
