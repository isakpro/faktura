using Faktura.Domain.PublicApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class ApiKeyDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string Prefix { get; set; } = "";
    public List<string> Scopes { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    [BsonIgnoreIfNull] public DateTime? LastUsedAt { get; set; }
    [BsonIgnoreIfNull] public DateTime? RevokedAt { get; set; }

    public static ApiKeyDocument FromDomain(ApiKey k) => new()
    {
        Id = k.Id,
        TenantId = k.TenantId,
        Name = k.Name,
        KeyHash = k.KeyHash,
        Prefix = k.Prefix,
        Scopes = k.Scopes.ToList(),
        CreatedAt = k.CreatedAt,
        LastUsedAt = k.LastUsedAt,
        RevokedAt = k.RevokedAt
    };

    public ApiKey ToDomain() => new(Id, TenantId, Name, KeyHash, Prefix, Scopes, CreatedAt, LastUsedAt, RevokedAt);
}
