using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Faktura.Infrastructure.Security;

/// <summary>Custom claim types carried by the access token.</summary>
public static class FakturaClaims
{
    public const string TenantId = "tenantId";
    public const string Role = "role";
    public const string Plan = "plan";
}

/// <summary>Issues HS256 access tokens and opaque, hashed refresh tokens.</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(User user, Organization organization)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(FakturaClaims.TenantId, organization.Id),
            new Claim(FakturaClaims.Role, user.Role.ToString()),
            new Claim(FakturaClaims.Plan, organization.Plan.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // notBefore utelämnas medvetet: exp är skyddet, och ett nbf från en styrbar test-
        // klocka skulle ligga i framtiden relativt JwtBearers verkliga valideringstid.
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: null,
            expires: expires,
            signingCredentials: _credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(encoded, expires);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenValue(raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
