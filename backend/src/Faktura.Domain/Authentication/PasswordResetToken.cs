namespace Faktura.Domain.Authentication;

/// <summary>Engångstoken för lösenordsåterställning (spec 011). Endast hashen lagras; 1 h giltighet.</summary>
public sealed class PasswordResetToken
{
    public string Id { get; }
    public string TenantId { get; }
    public string UserId { get; }
    public string TokenHash { get; }
    public DateTime ExpiresAt { get; }
    public DateTime? UsedAt { get; private set; }

    public PasswordResetToken(string id, string tenantId, string userId, string tokenHash, DateTime expiresAt, DateTime? usedAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        UsedAt = usedAt;
    }

    public static PasswordResetToken Issue(string id, string tenantId, string userId, string tokenHash, DateTime now)
        => new(id, tenantId, userId, tokenHash, now.AddHours(1), usedAt: null);

    public bool IsActive(DateTime now) => UsedAt is null && ExpiresAt > now;

    public void MarkUsed(DateTime when) => UsedAt ??= when;
}
