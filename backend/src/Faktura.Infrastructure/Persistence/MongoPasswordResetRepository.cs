using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class PasswordResetDocument
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
    [BsonIgnoreIfNull] public DateTime? UsedAt { get; set; }

    public static PasswordResetDocument FromDomain(PasswordResetToken t) => new()
    {
        Id = t.Id,
        TenantId = t.TenantId,
        UserId = t.UserId,
        TokenHash = t.TokenHash,
        ExpiresAt = t.ExpiresAt,
        UsedAt = t.UsedAt
    };

    public PasswordResetToken ToDomain() => new(Id, TenantId, UserId, TokenHash, ExpiresAt, UsedAt);
}

internal sealed class MongoPasswordResetRepository : IPasswordResetRepository
{
    private readonly MongoContext _context;

    public MongoPasswordResetRepository(MongoContext context) => _context = context;

    public Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
        => _context.PasswordResets.InsertOneAsync(PasswordResetDocument.FromDomain(token), cancellationToken: ct);

    public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var doc = await _context.PasswordResets.Find(d => d.TokenHash == tokenHash).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default)
        => _context.PasswordResets.ReplaceOneAsync(d => d.Id == token.Id,
            PasswordResetDocument.FromDomain(token), cancellationToken: ct);
}
