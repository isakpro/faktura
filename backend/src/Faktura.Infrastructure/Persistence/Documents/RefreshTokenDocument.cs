using Faktura.Domain.Authentication;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class RefreshTokenDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = "";

    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public static RefreshTokenDocument FromDomain(RefreshTokenRecord r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        UserId = r.UserId,
        TokenHash = r.TokenHash,
        ExpiresAt = r.ExpiresAt,
        RevokedAt = r.RevokedAt
    };

    public RefreshTokenRecord ToDomain() => new(Id, TenantId, UserId, TokenHash, ExpiresAt, RevokedAt);
}
