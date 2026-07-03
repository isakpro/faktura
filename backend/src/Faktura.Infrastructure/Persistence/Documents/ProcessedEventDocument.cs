using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

/// <summary>A processed webhook event id (used for idempotency). Id = provider event id.</summary>
internal sealed class ProcessedEventDocument
{
    [BsonId]
    public string Id { get; set; } = "";

    public string Type { get; set; } = "";
    public DateTime ProcessedAt { get; set; }
}
