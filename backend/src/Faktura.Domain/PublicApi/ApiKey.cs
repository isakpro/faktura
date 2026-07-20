using System.Security.Cryptography;
using System.Text;

namespace Faktura.Domain.PublicApi;

/// <summary>Namngivna scopes en API-nyckel kan beviljas (spec 016).</summary>
public static class ApiScopes
{
    public const string InvoicesRead = "invoices:read";
    public const string CustomersRead = "customers:read";
    public const string CustomersWrite = "customers:write";

    public static readonly IReadOnlyList<string> All = [InvoicesRead, CustomersRead, CustomersWrite];
}

/// <summary>
/// Genererar och hashar rå API-nycklar. Den råa nyckeln visas endast vid skapandet — därefter
/// lagras bara hashen (SHA-256) plus ett kort prefix för identifiering i UI:t.
/// </summary>
public static class ApiKeyGenerator
{
    public static (string RawKey, string Prefix) New()
    {
        var raw = "fkt_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        return (raw, raw[..16]);
    }

    public static string Hash(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
}

/// <summary>En API-nyckel för tenant-scoped åtkomst till det publika API:et (spec 016).</summary>
public sealed class ApiKey
{
    public string Id { get; }
    public string TenantId { get; }
    public string Name { get; }
    public string KeyHash { get; }
    public string Prefix { get; }
    public IReadOnlyList<string> Scopes { get; }
    public DateTime CreatedAt { get; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public ApiKey(string id, string tenantId, string name, string keyHash, string prefix,
        IEnumerable<string> scopes, DateTime createdAt, DateTime? lastUsedAt = null, DateTime? revokedAt = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        KeyHash = keyHash;
        Prefix = prefix;
        Scopes = scopes.ToList();
        CreatedAt = createdAt;
        LastUsedAt = lastUsedAt;
        RevokedAt = revokedAt;
    }

    public static ApiKey CreateNew(string id, string tenantId, string name, string rawKey, IEnumerable<string> scopes, DateTime now)
        => new(id, tenantId, name, ApiKeyGenerator.Hash(rawKey), rawKey[..16], scopes, now);

    public void MarkUsed(DateTime now) => LastUsedAt = now;

    public void Revoke(DateTime now) => RevokedAt = now;

    public bool HasScope(string scope) => Scopes.Contains(scope);
}
