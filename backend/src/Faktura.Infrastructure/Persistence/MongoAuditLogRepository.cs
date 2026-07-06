using Faktura.Domain.Abstractions;
using Faktura.Domain.Auditing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class AuditEntryDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string ActorEmail { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public int StatusCode { get; set; }
    public DateTime OccurredAt { get; set; }

    public static AuditEntryDocument FromDomain(AuditEntry e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ActorEmail = e.ActorEmail,
        Method = e.Method,
        Path = e.Path,
        StatusCode = e.StatusCode,
        OccurredAt = e.OccurredAt
    };

    public AuditEntry ToDomain() => new(Id, TenantId, ActorEmail, Method, Path, StatusCode, OccurredAt);
}

/// <summary>Append-only: exponerar endast insert + tenant-scoped läsning (spec 008).</summary>
internal sealed class MongoAuditLogRepository : TenantScopedRepository<AuditEntryDocument>, IAuditLogRepository
{
    public MongoAuditLogRepository(MongoContext context) : base(context.AuditLog) { }

    public Task AddAsync(AuditEntry entry, CancellationToken ct = default)
        => InsertAsync(AuditEntryDocument.FromDomain(entry), ct);

    public async Task<IReadOnlyList<AuditEntry>> ListLatestAsync(string tenantId, int limit, CancellationToken ct = default)
    {
        var docs = await Collection
            .Find(d => d.TenantId == tenantId)
            .SortByDescending(d => d.OccurredAt)
            .Limit(limit)
            .ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }
}
