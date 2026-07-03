using Faktura.Domain.Abstractions;
using Faktura.Domain.Users;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoInvitationRepository : TenantScopedRepository<InvitationDocument>, IInvitationRepository
{
    public MongoInvitationRepository(MongoContext context) : base(context.Invitations) { }

    public Task AddAsync(Invitation invitation, CancellationToken ct = default)
        => InsertAsync(InvitationDocument.FromDomain(invitation), ct);

    public async Task<IReadOnlyList<Invitation>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public async Task<Invitation?> GetByIdAsync(string tenantId, string invitationId, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, invitationId, ct))?.ToDomain();

    public Task UpdateAsync(Invitation invitation, CancellationToken ct = default)
        => ReplaceAsync(invitation.TenantId, invitation.Id, InvitationDocument.FromDomain(invitation), ct);

    public async Task<bool> HasPendingForEmailAsync(string tenantId, string normalizedEmail, CancellationToken ct = default)
    {
        var count = await CountAsync(tenantId,
            Builders<InvitationDocument>.Filter.And(
                Builders<InvitationDocument>.Filter.Eq(d => d.Email, normalizedEmail),
                Builders<InvitationDocument>.Filter.Eq(d => d.Status, InvitationStatus.Pending)),
            ct);
        return count > 0;
    }

    // Global lookup by unique token hash (accept happens before authentication).
    public async Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var doc = await Collection.Find(d => d.TokenHash == tokenHash).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }
}
