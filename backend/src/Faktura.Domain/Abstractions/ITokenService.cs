using Faktura.Domain.Organizations;
using Faktura.Domain.Users;

namespace Faktura.Domain.Abstractions;

/// <summary>An issued access token and when it expires (UTC).</summary>
public readonly record struct AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>A freshly generated refresh token: the raw value handed to the client and its stored hash.</summary>
public readonly record struct RefreshTokenValue(string Raw, string Hash);

/// <summary>
/// Issues signed access tokens (JWT) whose claims carry userId, tenantId and role, plus
/// opaque refresh tokens. The server is the sole authority over identity and role.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user, Organization organization);

    RefreshTokenValue CreateRefreshToken();

    /// <summary>Hashes a raw refresh token for lookup/verification.</summary>
    string HashRefreshToken(string rawToken);
}
