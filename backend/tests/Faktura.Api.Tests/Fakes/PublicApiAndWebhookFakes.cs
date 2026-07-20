using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Faktura.Domain.PublicApi;
using Faktura.Domain.Webhooks;

namespace Faktura.Api.Tests.Fakes;

public sealed class InMemoryApiKeyRepository : IApiKeyRepository
{
    private readonly ConcurrentDictionary<string, ApiKey> _items = new();

    public Task AddAsync(ApiKey key, CancellationToken ct = default)
    {
        _items[key.Id] = key;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ApiKey>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApiKey>>(_items.Values.Where(k => k.TenantId == tenantId).ToList());

    public Task<ApiKey?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(k => k.Id == id && k.TenantId == tenantId));

    public Task UpdateAsync(ApiKey key, CancellationToken ct = default)
    {
        _items[key.Id] = key;
        return Task.CompletedTask;
    }

    public Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(k => k.KeyHash == keyHash));
}

public sealed class InMemoryWebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly ConcurrentDictionary<string, WebhookEndpoint> _items = new();

    public Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _items[endpoint.Id] = endpoint;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookEndpoint>>(_items.Values.Where(e => e.TenantId == tenantId).ToList());

    public Task<WebhookEndpoint?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => Task.FromResult(_items.Values.FirstOrDefault(e => e.Id == id && e.TenantId == tenantId));

    public Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryWebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly ConcurrentDictionary<string, WebhookDelivery> _items = new();

    public Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _items[delivery.Id] = delivery;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(string tenantId, string endpointId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookDelivery>>(
            _items.Values.Where(d => d.TenantId == tenantId && d.EndpointId == endpointId).ToList());
}

public sealed record CapturedWebhook(string TenantId, string EventType, object Payload);

/// <summary>Fångar dispatchade händelser i minnet i stället för att göra riktiga HTTP-anrop.</summary>
public sealed class InMemoryWebhookDispatcher : IWebhookDispatcher
{
    private readonly ConcurrentQueue<CapturedWebhook> _dispatched = new();
    public IReadOnlyList<CapturedWebhook> Dispatched => _dispatched.ToList();

    public Task DispatchAsync(string tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        _dispatched.Enqueue(new CapturedWebhook(tenantId, eventType, payload));
        return Task.CompletedTask;
    }
}
