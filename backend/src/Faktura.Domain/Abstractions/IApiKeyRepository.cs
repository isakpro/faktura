using Faktura.Domain.PublicApi;

namespace Faktura.Domain.Abstractions;

public interface IApiKeyRepository
{
    Task AddAsync(ApiKey key, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task<ApiKey?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task UpdateAsync(ApiKey key, CancellationToken ct = default);

    /// <summary>Systemkontext (spec 016): slår upp en nyckel på dess hash, utan tenant-filter — hashen är sökvägen in.</summary>
    Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default);
}
