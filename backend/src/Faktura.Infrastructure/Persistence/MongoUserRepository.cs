using Faktura.Domain.Abstractions;
using Faktura.Domain.Users;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoUserRepository : IUserRepository
{
    private readonly MongoContext _context;

    public MongoUserRepository(MongoContext context) => _context = context;

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => await _context.Users.Find(u => u.Email == normalizedEmail).AnyAsync(ct);

    public async Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var doc = await _context.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        // Tenant-scoped: both the id AND the tenant must match (isolation, FR-007/009).
        var doc = await _context.Users
            .Find(u => u.Id == userId && u.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<int> CountByTenantAsync(string tenantId, CancellationToken ct = default)
        => (int)await _context.Users.CountDocumentsAsync(u => u.TenantId == tenantId, cancellationToken: ct);

    public Task AddAsync(User user, CancellationToken ct = default)
        => _context.Users.InsertOneAsync(UserDocument.FromDomain(user), cancellationToken: ct);
}
