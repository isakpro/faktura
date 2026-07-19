using Faktura.Domain.Authentication;

namespace Faktura.Domain.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshTokenRecord record, CancellationToken ct = default);

    /// <summary>Lookup by token hash (the raw token is never stored).</summary>
    Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    Task UpdateAsync(RefreshTokenRecord record, CancellationToken ct = default);

    /// <summary>Återkallar användarens samtliga aktiva refresh-tokens (lösenordsbyte, spec 011).</summary>
    Task RevokeAllForUserAsync(string tenantId, string userId, DateTime when, CancellationToken ct = default);
}
