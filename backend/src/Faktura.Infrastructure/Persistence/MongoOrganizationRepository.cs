using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoOrganizationRepository : IOrganizationRepository
{
    private readonly MongoContext _context;

    public MongoOrganizationRepository(MongoContext context) => _context = context;

    public Task AddAsync(Organization organization, CancellationToken ct = default)
        => _context.Organizations.InsertOneAsync(OrganizationDocument.FromDomain(organization), cancellationToken: ct);

    public async Task<Organization?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        var doc = await _context.Organizations
            .Find(o => o.Id == tenantId)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<Organization?> GetByStripeCustomerAsync(string customerId, CancellationToken ct = default)
    {
        var doc = await _context.Organizations
            .Find(o => o.StripeCustomerId == customerId)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public Task UpdateAsync(Organization organization, CancellationToken ct = default)
        => _context.Organizations.ReplaceOneAsync(
            o => o.Id == organization.Id,
            OrganizationDocument.FromDomain(organization),
            cancellationToken: ct);
}
