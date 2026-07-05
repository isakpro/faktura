using System.Security.Claims;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Auditing;
using Faktura.Infrastructure.Security;

namespace Faktura.Api.Auth;

/// <summary>
/// Aktivitetslogg (spec 008): loggar varje autentiserat muterande API-anrop (POST/PUT/DELETE)
/// med aktör, åtgärd och status. Middleware-ansatsen gör loggningen enhetlig — ingen enskild
/// tjänst kan glömma den. Fel i loggningen får aldrig påverka svaret (FR-003).
/// </summary>
public sealed class AuditMiddleware
{
    private static readonly string[] MutatingMethods = ["POST", "PUT", "DELETE"];

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        try
        {
            if (!MutatingMethods.Contains(context.Request.Method)) return;
            if (context.User.Identity?.IsAuthenticated != true) return; // anonyma anrop loggas ej
            var path = context.Request.Path.Value ?? "";
            if (!path.StartsWith("/api/") || path.StartsWith("/api/audit")) return;

            var tenantId = context.User.FindFirstValue(FakturaClaims.TenantId);
            if (string.IsNullOrEmpty(tenantId)) return;

            var repo = context.RequestServices.GetRequiredService<IAuditLogRepository>();
            var ids = context.RequestServices.GetRequiredService<IIdGenerator>();
            var clock = context.RequestServices.GetRequiredService<IClock>();

            await repo.AddAsync(new AuditEntry(
                ids.NewId(),
                tenantId,
                context.User.FindFirstValue("email") ?? "",
                context.Request.Method,
                path,
                context.Response.StatusCode,
                clock.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for {Path}", context.Request.Path);
        }
    }
}
