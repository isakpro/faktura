using Faktura.Domain.Users;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public UserRole Role { get; set; }

    [BsonRepresentation(BsonType.String)]
    public UserStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public static UserDocument FromDomain(User u) => new()
    {
        Id = u.Id,
        TenantId = u.TenantId,
        Email = u.Email,
        PasswordHash = u.PasswordHash,
        Role = u.Role,
        Status = u.Status,
        CreatedAt = u.CreatedAt
    };

    public User ToDomain() => new(Id, TenantId, Email, PasswordHash, Role, Status, CreatedAt);
}
