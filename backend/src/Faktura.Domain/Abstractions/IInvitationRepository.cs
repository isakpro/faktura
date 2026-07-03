using Faktura.Domain.Users;

namespace Faktura.Domain.Abstractions;

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken ct = default);

    /// <summary>Tenant-scoped list of invitations.</summary>
    Task<IReadOnlyList<Invitation>> ListByTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Tenant-scoped lookup by id.</summary>
    Task<Invitation?> GetByIdAsync(string tenantId, string invitationId, CancellationToken ct = default);

    /// <summary>Global lookup by token hash (used during accept, before authentication).</summary>
    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>True if a pending invitation already exists for the email in the tenant.</summary>
    Task<bool> HasPendingForEmailAsync(string tenantId, string normalizedEmail, CancellationToken ct = default);

    Task UpdateAsync(Invitation invitation, CancellationToken ct = default);
}
