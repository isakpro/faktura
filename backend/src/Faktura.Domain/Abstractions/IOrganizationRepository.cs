using Faktura.Domain.Organizations;

namespace Faktura.Domain.Abstractions;

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken ct = default);

    Task<Organization?> GetByIdAsync(string tenantId, CancellationToken ct = default);

    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
