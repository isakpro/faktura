using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MongoContext _context;

    public MongoRefreshTokenRepository(MongoContext context) => _context = context;

    public Task AddAsync(RefreshTokenRecord record, CancellationToken ct = default)
        => _context.RefreshTokens.InsertOneAsync(RefreshTokenDocument.FromDomain(record), cancellationToken: ct);

    public async Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var doc = await _context.RefreshTokens.Find(r => r.TokenHash == tokenHash).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task RevokeAllForUserAsync(string tenantId, string userId, DateTime when, CancellationToken ct = default)
        => _context.RefreshTokens.UpdateManyAsync(
            r => r.TenantId == tenantId && r.UserId == userId && r.RevokedAt == null,
            Builders<RefreshTokenDocument>.Update.Set(r => r.RevokedAt, when),
            cancellationToken: ct);

    public Task UpdateAsync(RefreshTokenRecord record, CancellationToken ct = default)
        => _context.RefreshTokens.ReplaceOneAsync(
            r => r.Id == record.Id,
            RefreshTokenDocument.FromDomain(record),
            cancellationToken: ct);
}
