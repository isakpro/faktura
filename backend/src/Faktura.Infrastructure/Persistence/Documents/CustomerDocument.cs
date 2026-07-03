using Faktura.Domain.Customers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class AddressDocument
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    public static AddressDocument? FromDomain(Address? a) => a is null
        ? null
        : new AddressDocument { Line1 = a.Line1, Line2 = a.Line2, PostalCode = a.PostalCode, City = a.City, Country = a.Country };

    public Address ToDomain() => new(Line1, Line2, PostalCode, City, Country);
}

internal sealed class CustomerDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? OrgNumber { get; set; }
    public string? VatNumber { get; set; }
    public AddressDocument? Address { get; set; }
    public int PaymentTermsDays { get; set; }

    [BsonRepresentation(BsonType.String)]
    public CustomerStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public static CustomerDocument FromDomain(Customer c) => new()
    {
        Id = c.Id,
        TenantId = c.TenantId,
        Name = c.Name,
        Email = c.Email,
        OrgNumber = c.OrgNumber,
        VatNumber = c.VatNumber,
        Address = AddressDocument.FromDomain(c.Address),
        PaymentTermsDays = c.PaymentTermsDays,
        Status = c.Status,
        CreatedAt = c.CreatedAt
    };

    public Customer ToDomain() => new(Id, TenantId, Name, Email, OrgNumber, VatNumber,
        Address?.ToDomain(), PaymentTermsDays, Status, CreatedAt);
}
