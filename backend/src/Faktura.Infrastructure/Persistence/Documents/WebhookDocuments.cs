using Faktura.Domain.Webhooks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class WebhookEndpointDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Url { get; set; } = "";
    public string Secret { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public static WebhookEndpointDocument FromDomain(WebhookEndpoint e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        Url = e.Url,
        Secret = e.Secret,
        CreatedAt = e.CreatedAt
    };

    public WebhookEndpoint ToDomain() => new(Id, TenantId, Url, Secret, CreatedAt);
}

internal sealed class WebhookDeliveryDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string EndpointId { get; set; } = "";

    public string EventType { get; set; } = "";
    public bool Success { get; set; }

    [BsonIgnoreIfNull] public int? StatusCode { get; set; }
    [BsonIgnoreIfNull] public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }

    public static WebhookDeliveryDocument FromDomain(WebhookDelivery d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        EndpointId = d.EndpointId,
        EventType = d.EventType,
        Success = d.Success,
        StatusCode = d.StatusCode,
        Error = d.Error,
        CreatedAt = d.CreatedAt
    };

    public WebhookDelivery ToDomain() => new(Id, TenantId, EndpointId, EventType, Success, StatusCode, Error, CreatedAt);
}
