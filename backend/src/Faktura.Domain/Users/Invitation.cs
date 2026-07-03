using Faktura.Domain.Common;

namespace Faktura.Domain.Users;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2
}

/// <summary>A pending invitation for an email to join a tenant with a given role.</summary>
public sealed class Invitation
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string Email { get; private set; }
    public UserRole Role { get; private set; }
    public string TokenHash { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Invitation(string id, string tenantId, string email, UserRole role, string tokenHash,
        InvitationStatus status, DateTime expiresAt, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        Status = status;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    /// <summary>Creates a pending invitation. Owner cannot be invited (it is the founder role).</summary>
    public static Result<Invitation> CreateNew(
        string id, string tenantId, string normalizedEmail, UserRole role, string tokenHash, DateTime now, TimeSpan ttl)
    {
        if (role == UserRole.Owner)
            return Result.Failure<Invitation>(Error.Validation("Owner-rollen kan inte bjudas in."));

        return Result.Success(new Invitation(
            id, tenantId, normalizedEmail, role, tokenHash,
            InvitationStatus.Pending, now.Add(ttl), now));
    }

    public bool IsAcceptable(DateTime now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    public Result Accept(DateTime now)
    {
        if (!IsAcceptable(now))
            return Result.Failure(Error.InvitationInvalid());
        Status = InvitationStatus.Accepted;
        return Result.Success();
    }

    public void Revoke()
    {
        if (Status == InvitationStatus.Pending)
            Status = InvitationStatus.Revoked;
    }
}
