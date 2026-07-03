namespace Faktura.Domain.Users;

/// <summary>Role of a user within their organization. Enforced server-side (RBAC).</summary>
public enum UserRole
{
    Member = 0,
    Admin = 1,
    Owner = 2
}
