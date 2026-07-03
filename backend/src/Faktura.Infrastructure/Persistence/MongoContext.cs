using Faktura.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

/// <summary>
/// Holds the MongoDB collections and creates indexes. Index creation is explicit
/// (called at startup) rather than on every request.
/// </summary>
public sealed class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IOptions<MongoOptions> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.Database);
    }

    internal IMongoCollection<OrganizationDocument> Organizations => _database.GetCollection<OrganizationDocument>("organizations");
    internal IMongoCollection<UserDocument> Users => _database.GetCollection<UserDocument>("users");
    internal IMongoCollection<RefreshTokenDocument> RefreshTokens => _database.GetCollection<RefreshTokenDocument>("refreshTokens");

    /// <summary>Creates indexes described in data-model.md. Safe to call repeatedly.</summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        await Users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_user_email" }),
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(u => u.TenantId).Ascending(u => u.Role),
                new CreateIndexOptions { Name = "ix_user_tenant_role" })
        }, ct);

        await RefreshTokens.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshTokenDocument>(
                Builders<RefreshTokenDocument>.IndexKeys.Ascending(r => r.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_refresh_hash" }),
            new CreateIndexModel<RefreshTokenDocument>(
                Builders<RefreshTokenDocument>.IndexKeys.Ascending(r => r.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_refresh_expires", ExpireAfter = TimeSpan.Zero })
        }, ct);
    }
}
