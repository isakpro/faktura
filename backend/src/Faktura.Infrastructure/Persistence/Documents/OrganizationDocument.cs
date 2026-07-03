using Faktura.Domain.Organizations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class OrganizationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public PlanTier Plan { get; set; }

    [BsonRepresentation(BsonType.String)]
    public SubscriptionStatus SubscriptionStatus { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public int SeatLimit { get; set; }
    public DateTime CreatedAt { get; set; }

    public static OrganizationDocument FromDomain(Organization o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Plan = o.Plan,
        SubscriptionStatus = o.SubscriptionStatus,
        StripeCustomerId = o.StripeCustomerId,
        StripeSubscriptionId = o.StripeSubscriptionId,
        SeatLimit = o.SeatLimit,
        CreatedAt = o.CreatedAt
    };

    public Organization ToDomain() => new(
        Id, Name, Plan, SubscriptionStatus, StripeCustomerId, StripeSubscriptionId, SeatLimit, CreatedAt);
}
