using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Faktura.Api.Features.Auth;

/// <summary>
/// Application service orchestrating registration, login, token refresh and logout.
/// Composes the pure domain logic with repositories and token issuance.
/// </summary>
public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly AccountRegistration _registration;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly ILoginThrottle _throttle;
    private readonly TokenIssuer _issuer;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IOrganizationRepository organizations,
        IRefreshTokenRepository refreshTokens,
        AccountRegistration registration,
        ITokenService tokens,
        IPasswordHasher passwordHasher,
        IClock clock,
        ILoginThrottle throttle,
        TokenIssuer issuer,
        ILogger<AuthService> logger)
    {
        _users = users;
        _organizations = organizations;
        _refreshTokens = refreshTokens;
        _registration = registration;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _throttle = throttle;
        _issuer = issuer;
        _logger = logger;
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
        return Result.Success(await _issuer.IssueAsync(owner, organization, ct));
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
        return Result.Success(await _issuer.IssueAsync(user, organization, ct));
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

        return Result.Success(await _issuer.IssuePairAsync(user, organization, ct));
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;
        var record = await _refreshTokens.GetByHashAsync(_tokens.HashRefreshToken(rawRefreshToken), ct);
        if (record is null) return;
        record.Revoke(_clock.UtcNow);
        await _refreshTokens.UpdateAsync(record, ct);
    }
}
