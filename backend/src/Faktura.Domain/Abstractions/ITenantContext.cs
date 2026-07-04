using Faktura.Domain.Users;

namespace Faktura.Domain.Abstractions;

/// <summary>
/// The current request's tenant/user, derived ONLY from the authenticated JWT.
/// The data layer uses <see cref="TenantId"/> to enforce isolation; it is never taken
/// from the request body or query string.
/// </summary>
public interface ITenantContext
{
    /// <summary>True when a valid authenticated principal is present.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The tenant (organization) id of the current user. Throws when unauthenticated.</summary>
    string TenantId { get; }

    /// <summary>The current user's id. Throws when unauthenticated.</summary>
    string UserId { get; }

    /// <summary>The current user's email (from the JWT), or null if absent.</summary>
    string? Email { get; }

    /// <summary>The current user's role.</summary>
    UserRole Role { get; }
}
