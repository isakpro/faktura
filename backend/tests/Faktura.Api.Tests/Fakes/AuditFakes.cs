using Faktura.Domain.Abstractions;
using Faktura.Domain.Auditing;

namespace Faktura.Api.Tests.Fakes;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly List<AuditEntry> _items = new();

    public Task AddAsync(AuditEntry entry, CancellationToken ct = default)
    {
        lock (_items) _items.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> ListLatestAsync(string tenantId, int limit, CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult<IReadOnlyList<AuditEntry>>(
                _items.Where(e => e.TenantId == tenantId)
                    .OrderByDescending(e => e.OccurredAt).Take(limit).ToList());
    }
}
