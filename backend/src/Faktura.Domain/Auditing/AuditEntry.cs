namespace Faktura.Domain.Auditing;

/// <summary>
/// En post i organisationens aktivitetslogg — append-only (spec 008). Fångas automatiskt av
/// API:ts audit-middleware för autentiserade muterande anrop.
/// </summary>
public sealed record AuditEntry(
    string Id,
    string TenantId,
    string ActorEmail,
    string Method,
    string Path,
    int StatusCode,
    DateTime OccurredAt);
