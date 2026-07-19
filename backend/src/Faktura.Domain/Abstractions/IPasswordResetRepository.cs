using Faktura.Domain.Authentication;

namespace Faktura.Domain.Abstractions;

public interface IPasswordResetRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);

    /// <summary>Global uppslagning på token-hash (flödet är anonymt).</summary>
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default);
}
