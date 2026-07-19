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

    [BsonIgnoreIfNull]
    public InvoiceProfileDocument? Profile { get; set; }

    public static OrganizationDocument FromDomain(Organization o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Plan = o.Plan,
        SubscriptionStatus = o.SubscriptionStatus,
        StripeCustomerId = o.StripeCustomerId,
        StripeSubscriptionId = o.StripeSubscriptionId,
        SeatLimit = o.SeatLimit,
        CreatedAt = o.CreatedAt,
        Profile = InvoiceProfileDocument.FromDomain(o.Profile)
    };

    public Organization ToDomain() => new(
        Id, Name, Plan, SubscriptionStatus, StripeCustomerId, StripeSubscriptionId, SeatLimit, CreatedAt,
        Profile?.ToDomain());
}

internal sealed class InvoiceProfileDocument
{
    public string? OrgNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Bankgiro { get; set; }
    public string? Plusgiro { get; set; }
    public bool FSkatt { get; set; }

    public static InvoiceProfileDocument? FromDomain(InvoiceProfile? p) => p is null ? null : new()
    {
        OrgNumber = p.OrgNumber,
        AddressLine = p.AddressLine,
        PostalCode = p.PostalCode,
        City = p.City,
        Bankgiro = p.Bankgiro,
        Plusgiro = p.Plusgiro,
        FSkatt = p.FSkatt
    };

    public InvoiceProfile ToDomain() => new(OrgNumber, AddressLine, PostalCode, City, Bankgiro, Plusgiro, FSkatt);
}
