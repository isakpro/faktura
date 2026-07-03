namespace Faktura.Domain.Users;

/// <summary>
/// A user account belonging to exactly one organization (tenant) in v1.
/// <see cref="TenantId"/> is the isolation key enforced by the data layer.
/// </summary>
public sealed class User
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For persistence hydration.
    public User(
        string id,
        string tenantId,
        string email,
        string passwordHash,
        UserRole role,
        UserStatus status,
        DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
    }

    /// <summary>Creates the first user of an organization as its Owner.</summary>
    public static User CreateOwner(string id, string tenantId, string normalizedEmail, string passwordHash, DateTime now)
        => new(id, tenantId, normalizedEmail, passwordHash, UserRole.Owner, UserStatus.Active, now);

    /// <summary>Creates an additional member/admin (used from the invitation flow in US3).</summary>
    public static User CreateMember(string id, string tenantId, string normalizedEmail, string passwordHash, UserRole role, DateTime now)
        => new(id, tenantId, normalizedEmail, passwordHash, role, UserStatus.Active, now);

    /// <summary>Changes the user's role (authorization is enforced by <see cref="MembershipRules"/>).</summary>
    public void ChangeRole(UserRole role) => Role = role;
}
