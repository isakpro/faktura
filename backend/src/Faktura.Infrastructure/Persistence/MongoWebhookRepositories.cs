using Faktura.Domain.Abstractions;
using Faktura.Domain.Webhooks;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class MongoWebhookEndpointRepository : TenantScopedRepository<WebhookEndpointDocument>, IWebhookEndpointRepository
{
    public MongoWebhookEndpointRepository(MongoContext context) : base(context.WebhookEndpoints) { }

    public Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
        => InsertAsync(WebhookEndpointDocument.FromDomain(endpoint), ct);

    public async Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(string tenantId, CancellationToken ct = default)
        => (await ListAsync(tenantId, ct)).Select(d => d.ToDomain()).ToList();

    public async Task<WebhookEndpoint?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
        => (await FindByIdAsync(tenantId, id, ct))?.ToDomain();

    public Task DeleteAsync(string tenantId, string id, CancellationToken ct = default)
        => DeleteByIdAsync(tenantId, id, ct);
}

internal sealed class MongoWebhookDeliveryRepository : TenantScopedRepository<WebhookDeliveryDocument>, IWebhookDeliveryRepository
{
    public MongoWebhookDeliveryRepository(MongoContext context) : base(context.WebhookDeliveries) { }

    public Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
        => InsertAsync(WebhookDeliveryDocument.FromDomain(delivery), ct);

    public async Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(string tenantId, string endpointId, CancellationToken ct = default)
    {
        var docs = await ListAsync(tenantId, ct);
        return docs.Where(d => d.EndpointId == endpointId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => d.ToDomain())
            .ToList();
    }
}
