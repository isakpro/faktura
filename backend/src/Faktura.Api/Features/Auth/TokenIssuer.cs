using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;
using Faktura.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Auth;

/// <summary>Issues access + refresh tokens and persists the refresh token. Shared by login and invite-accept.</summary>
public sealed class TokenIssuer
{
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly JwtOptions _jwt;

    public TokenIssuer(
        ITokenService tokens,
        IRefreshTokenRepository refreshTokens,
        IIdGenerator ids,
        IClock clock,
        IOptions<JwtOptions> jwt)
    {
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _ids = ids;
        _clock = clock;
        _jwt = jwt.Value;
    }

    public async Task<AuthResponse> IssueAsync(User user, Organization organization, CancellationToken ct)
    {
        var access = _tokens.CreateAccessToken(user, organization);
        var refresh = await PersistRefreshTokenAsync(user, ct);
        return new AuthResponse(access.Token, refresh, access.ExpiresAtUtc, user.ToDto(), organization.ToDto());
    }

    public async Task<TokenResponse> IssuePairAsync(User user, Organization organization, CancellationToken ct)
    {
        var access = _tokens.CreateAccessToken(user, organization);
        var refresh = await PersistRefreshTokenAsync(user, ct);
        return new TokenResponse(access.Token, refresh, access.ExpiresAtUtc);
    }

    private async Task<string> PersistRefreshTokenAsync(User user, CancellationToken ct)
    {
        var value = _tokens.CreateRefreshToken();
        var expires = _clock.UtcNow.AddDays(_jwt.RefreshTokenDays);
        var record = RefreshTokenRecord.Issue(_ids.NewId(), user.TenantId, user.Id, value.Hash, expires);
        await _refreshTokens.AddAsync(record, ct);
        return value.Raw;
    }
}
