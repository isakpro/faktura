using Faktura.Domain.Webhooks;

namespace Faktura.Domain.Abstractions;

public interface IWebhookEndpointRepository
{
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task<WebhookEndpoint?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken ct = default);
}

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(string tenantId, string endpointId, CancellationToken ct = default);
}

/// <summary>
/// Skickar ut en händelse till tenantens registrerade webhook-mottagare (spec 016).
/// Domänlagret känner bara till kontraktet — HTTP/HMAC/loggning sköts av implementationen.
/// </summary>
public interface IWebhookDispatcher
{
    Task DispatchAsync(string tenantId, string eventType, object payload, CancellationToken ct = default);
}
