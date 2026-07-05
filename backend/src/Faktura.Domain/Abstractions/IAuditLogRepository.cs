using Faktura.Domain.Auditing;

namespace Faktura.Domain.Abstractions;

/// <summary>Append-only aktivitetslogg (spec 008): inga update-/delete-operationer exponeras.</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct = default);

    /// <summary>De senaste posterna för organisationen, nyast först.</summary>
    Task<IReadOnlyList<AuditEntry>> ListLatestAsync(string tenantId, int limit, CancellationToken ct = default);
}
