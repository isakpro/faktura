using System.Security.Cryptography;

namespace Faktura.Domain.Webhooks;

/// <summary>En tenants mottagar-URL för webhook-händelser (spec 016).</summary>
public sealed class WebhookEndpoint
{
    public string Id { get; }
    public string TenantId { get; }
    public string Url { get; }
    public string Secret { get; }
    public DateTime CreatedAt { get; }

    public WebhookEndpoint(string id, string tenantId, string url, string secret, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Url = url;
        Secret = secret;
        CreatedAt = createdAt;
    }

    public static WebhookEndpoint CreateNew(string id, string tenantId, string url, DateTime now)
        => new(id, tenantId, url, GenerateSecret(), now);

    private static string GenerateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}

/// <summary>Loggpost för ett leveransförsök (spec 016) — append-only, som e-post-/påminnelseloggarna.</summary>
public sealed class WebhookDelivery
{
    public string Id { get; }
    public string TenantId { get; }
    public string EndpointId { get; }
    public string EventType { get; }
    public bool Success { get; }
    public int? StatusCode { get; }
    public string? Error { get; }
    public DateTime CreatedAt { get; }

    public WebhookDelivery(string id, string tenantId, string endpointId, string eventType,
        bool success, int? statusCode, string? error, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        EndpointId = endpointId;
        EventType = eventType;
        Success = success;
        StatusCode = statusCode;
        Error = error;
        CreatedAt = createdAt;
    }
}

/// <summary>HMAC-SHA256-signering av webhook-nyttolaster (spec 016) — ren funktion, testbar isolerat.</summary>
public static class WebhookSigner
{
    public static string Sign(string secret, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();
}
