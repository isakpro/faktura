using Faktura.Domain.Users;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class InvitationDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Email { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public UserRole Role { get; set; }

    public string TokenHash { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public InvitationStatus Status { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static InvitationDocument FromDomain(Invitation i) => new()
    {
        Id = i.Id,
        TenantId = i.TenantId,
        Email = i.Email,
        Role = i.Role,
        TokenHash = i.TokenHash,
        Status = i.Status,
        ExpiresAt = i.ExpiresAt,
        CreatedAt = i.CreatedAt
    };

    public Invitation ToDomain() => new(Id, TenantId, Email, Role, TokenHash, Status, ExpiresAt, CreatedAt);
}
