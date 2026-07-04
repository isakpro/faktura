using Faktura.Domain.Users;

namespace Faktura.Domain.Abstractions;

public interface IUserRepository
{
    /// <summary>Global uniqueness check used during registration (email is globally unique in v1).</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>Global lookup by email, used at login before a tenant is known.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>Tenant-scoped lookup by id.</summary>
    Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>Count of active users in a tenant (for seat-limit checks in US3).</summary>
    Task<int> CountByTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Count of Owners in a tenant (for the "at least one Owner" invariant).</summary>
    Task<int> CountOwnersAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Tenant-scoped list of all users in the organization.</summary>
    Task<IReadOnlyList<User>> ListByTenantAsync(string tenantId, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    Task UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>Tenant-scoped removal (both id and tenant must match).</summary>
    Task RemoveAsync(string tenantId, string userId, CancellationToken ct = default);
}
