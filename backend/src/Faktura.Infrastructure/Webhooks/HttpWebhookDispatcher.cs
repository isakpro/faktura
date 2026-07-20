using System.Text;
using System.Text.Json;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace Faktura.Infrastructure.Webhooks;

/// <summary>
/// Levererar webhook-händelser till tenantens registrerade mottagar-URL:er (spec 016):
/// signerar nyttolasten (HMAC-SHA256), försöker en gång till vid fel, loggar varje försök
/// (append-only, samma mönster som e-post-/påminnelseloggarna).
/// </summary>
public sealed class HttpWebhookDispatcher : IWebhookDispatcher
{
    private readonly IWebhookEndpointRepository _endpoints;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly ILogger<HttpWebhookDispatcher> _logger;

    public HttpWebhookDispatcher(IWebhookEndpointRepository endpoints, IWebhookDeliveryRepository deliveries,
        IHttpClientFactory httpFactory, IIdGenerator ids, IClock clock, ILogger<HttpWebhookDispatcher> logger)
    {
        _endpoints = endpoints;
        _deliveries = deliveries;
        _httpFactory = httpFactory;
        _ids = ids;
        _clock = clock;
        _logger = logger;
    }

    public async Task DispatchAsync(string tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        var endpoints = await _endpoints.ListByTenantAsync(tenantId, ct);
        if (endpoints.Count == 0) return;

        var body = JsonSerializer.Serialize(new { id = _ids.NewId(), type = eventType, occurredAt = _clock.UtcNow, data = payload });

        foreach (var endpoint in endpoints)
        {
            var signature = WebhookSigner.Sign(endpoint.Secret, body);
            var (success, statusCode, error) = await TrySendAsync(endpoint.Url, body, signature, ct);
            if (!success)
                (success, statusCode, error) = await TrySendAsync(endpoint.Url, body, signature, ct); // en retry

            if (!success)
                _logger.LogWarning("Webhook {EventType} to {Url} failed after retry: {Error}", eventType, endpoint.Url, error);

            await _deliveries.AddAsync(
                new WebhookDelivery(_ids.NewId(), tenantId, endpoint.Id, eventType, success, statusCode, error, _clock.UtcNow), ct);
        }
    }

    private async Task<(bool Success, int? StatusCode, string? Error)> TrySendAsync(
        string url, string body, string signature, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient("webhooks");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Faktura-Signature", signature);

            var response = await client.SendAsync(request, ct);
            return (response.IsSuccessStatusCode, (int)response.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
