namespace Faktura.Domain.Authentication;

/// <summary>A stored refresh token (only its hash is persisted). Rotated on use.</summary>
public sealed class RefreshTokenRecord
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public RefreshTokenRecord(string id, string tenantId, string userId, string tokenHash, DateTime expiresAt, DateTime? revokedAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
    }

    public static RefreshTokenRecord Issue(string id, string tenantId, string userId, string tokenHash, DateTime expiresAt)
        => new(id, tenantId, userId, tokenHash, expiresAt, revokedAt: null);

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTime when) => RevokedAt ??= when;
}
