using System.Security.Claims;
using System.Text.Encodings.Web;
using Faktura.Domain.Abstractions;
using Faktura.Domain.PublicApi;
using Faktura.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Faktura.Api.Auth;

/// <summary>
/// Autentiserar det publika API:et (spec 016) via header <c>X-Api-Key</c>. Bygger samma
/// claim-typer som JWT-flödet (<see cref="FakturaClaims"/> + "sub") så att befintlig
/// <see cref="ITenantContext"/> och tenant-scopade tjänster fungerar oförändrat; extra
/// claimet "scopes" avgör vilka publika endpoints nyckeln får anropa.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IApiKeyRepository _apiKeys;
    private readonly IClock _clock;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IApiKeyRepository apiKeys,
        IClock clock) : base(options, loggerFactory, encoder)
    {
        _apiKeys = apiKeys;
        _clock = clock;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var header) || string.IsNullOrWhiteSpace(header))
            return AuthenticateResult.NoResult();

        var hash = ApiKeyGenerator.Hash(header.ToString());
        var key = await _apiKeys.GetByHashAsync(hash, Context.RequestAborted);
        if (key is null || !key.IsActive)
            return AuthenticateResult.Fail("Ogiltig eller återkallad API-nyckel.");

        key.MarkUsed(_clock.UtcNow);
        await _apiKeys.UpdateAsync(key, Context.RequestAborted);

        var claims = new[]
        {
            new Claim("sub", key.Id),
            new Claim("email", ""),
            new Claim(FakturaClaims.TenantId, key.TenantId),
            new Claim(FakturaClaims.Role, "Member"),
            new Claim("scopes", string.Join(' ', key.Scopes)),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
