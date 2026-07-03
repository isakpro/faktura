using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;
using Faktura.Infrastructure.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Features.Auth;

/// <summary>
/// Application service orchestrating registration, login, token refresh and logout.
/// Composes the pure domain logic with repositories and the token service.
/// </summary>
public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly AccountRegistration _registration;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILoginThrottle _throttle;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtOptions _jwt;

    public AuthService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IRefreshTokenRepository refreshTokens,
        AccountRegistration registration,
        ITokenService tokens,
        IPasswordHasher passwordHasher,
        IIdGenerator ids,
        IClock clock,
        ILoginThrottle throttle,
        ILogger<AuthService> logger,
        IOptions<JwtOptions> jwt)
    {
        _users = users;
        _organizations = organizations;
        _refreshTokens = refreshTokens;
        _registration = registration;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _ids = ids;
        _clock = clock;
        _throttle = throttle;
        _logger = logger;
        _jwt = jwt.Value;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var built = _registration.Register(request.OrganizationName, request.Email, request.Password);
        if (built.IsFailure)
            return Result.Failure<AuthResponse>(built.Error);

        var (organization, owner) = (built.Value.Organization, built.Value.Owner);

        // Uniqueness is enforced here (domain stays pure). Do not leak whether the account exists.
        if (await _users.EmailExistsAsync(owner.Email, ct))
            return Result.Failure<AuthResponse>(Error.EmailAlreadyInUse());

        await _organizations.AddAsync(organization, ct);
        await _users.AddAsync(owner, ct);

        _logger.LogInformation("Organization {TenantId} registered with owner {UserId}", organization.Id, owner.Id);
        var response = await IssueTokensAsync(owner, organization, ct);
        return Result.Success(response);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = EmailAddress.Create(request.Email);
        if (email.IsFailure)
            return Result.Failure<AuthResponse>(Error.InvalidCredentials());

        var key = email.Value.Value;
        if (_throttle.IsBlocked(key, out var retryAfter))
        {
            _logger.LogWarning("Login blocked by throttle for {Email}", key);
            return Result.Failure<AuthResponse>(Error.TooManyAttempts(retryAfter));
        }

        var user = await _users.GetByEmailAsync(key, ct);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            _throttle.RecordFailure(key);
            _logger.LogWarning("Failed login attempt for {Email}", key);
            return Result.Failure<AuthResponse>(Error.InvalidCredentials());
        }

        var organization = await _organizations.GetByIdAsync(user.TenantId, ct);
        if (organization is null)
            return Result.Failure<AuthResponse>(Error.InvalidCredentials());

        _throttle.Reset(key);
        _logger.LogInformation("User {UserId} logged in (tenant {TenantId})", user.Id, organization.Id);
        var response = await IssueTokensAsync(user, organization, ct);
        return Result.Success(response);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return Result.Failure<TokenResponse>(Error.InvalidCredentials());

        var hash = _tokens.HashRefreshToken(rawRefreshToken);
        var record = await _refreshTokens.GetByHashAsync(hash, ct);
        var now = _clock.UtcNow;
        if (record is null || !record.IsActive(now))
            return Result.Failure<TokenResponse>(Error.InvalidCredentials());

        var user = await _users.GetByIdAsync(record.TenantId, record.UserId, ct);
        var organization = await _organizations.GetByIdAsync(record.TenantId, ct);
        if (user is null || organization is null)
            return Result.Failure<TokenResponse>(Error.InvalidCredentials());

        // Rotate: revoke the used token and issue a new pair.
        record.Revoke(now);
        await _refreshTokens.UpdateAsync(record, ct);

        var access = _tokens.CreateAccessToken(user, organization);
        var refresh = await PersistRefreshTokenAsync(user, ct);
        return Result.Success(new TokenResponse(access.Token, refresh, access.ExpiresAtUtc));
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;
        var record = await _refreshTokens.GetByHashAsync(_tokens.HashRefreshToken(rawRefreshToken), ct);
        if (record is null) return;
        record.Revoke(_clock.UtcNow);
        await _refreshTokens.UpdateAsync(record, ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Organization organization, CancellationToken ct)
    {
        var access = _tokens.CreateAccessToken(user, organization);
        var refresh = await PersistRefreshTokenAsync(user, ct);
        return new AuthResponse(access.Token, refresh, access.ExpiresAtUtc, Map(user), Map(organization));
    }

    private async Task<string> PersistRefreshTokenAsync(User user, CancellationToken ct)
    {
        var value = _tokens.CreateRefreshToken();
        var expires = _clock.UtcNow.AddDays(_jwt.RefreshTokenDays);
        var record = RefreshTokenRecord.Issue(_ids.NewId(), user.TenantId, user.Id, value.Hash, expires);
        await _refreshTokens.AddAsync(record, ct);
        return value.Raw;
    }

    internal static UserDto Map(User u) => new(u.Id, u.Email, u.Role.ToString());

    internal static OrganizationDto Map(Organization o) =>
        new(o.Id, o.Name, o.Plan.ToString(), o.SubscriptionStatus.ToString(), o.SeatLimit);
}
