using System.Security.Claims;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Users;
using Faktura.Infrastructure.Security;

namespace Faktura.Api.Auth;

/// <summary>
/// Derives the current tenant/user strictly from the authenticated JWT claims.
/// Never reads tenant information from the request body or query (constitution V).
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly ClaimsPrincipal? _user;

    public HttpTenantContext(IHttpContextAccessor accessor) => _user = accessor.HttpContext?.User;

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated == true;

    public string TenantId => Required(FakturaClaims.TenantId);

    public string UserId => Required("sub");

    public UserRole Role =>
        Enum.TryParse<UserRole>(_user?.FindFirstValue(FakturaClaims.Role), out var role)
            ? role
            : UserRole.Member;

    private string Required(string claimType)
    {
        var value = _user?.FindFirstValue(claimType);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Missing '{claimType}' claim; request is not authenticated.");
        return value;
    }
}
