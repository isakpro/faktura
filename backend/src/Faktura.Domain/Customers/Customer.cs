namespace Faktura.Domain.Customers;

public sealed record Address(string? Line1, string? Line2, string? PostalCode, string? City, string? Country);

public enum CustomerStatus { Active = 0, Archived = 1 }

/// <summary>En kund som tillhör en organisation (tenant).</summary>
public sealed class Customer
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Email { get; private set; }
    public string? OrgNumber { get; private set; }
    public string? VatNumber { get; private set; }
    public Address? Address { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Customer(string id, string tenantId, string name, string? email, string? orgNumber,
        string? vatNumber, Address? address, int paymentTermsDays, CustomerStatus status, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Email = email;
        OrgNumber = orgNumber;
        VatNumber = vatNumber;
        Address = address;
        PaymentTermsDays = paymentTermsDays;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Customer CreateNew(string id, string tenantId, string name, string? email, string? orgNumber,
        string? vatNumber, Address? address, int? paymentTermsDays, DateTime now)
        => new(id, tenantId, name.Trim(), email, orgNumber, vatNumber, address,
            paymentTermsDays is > 0 ? paymentTermsDays.Value : 30, CustomerStatus.Active, now);

    public void Update(string name, string? email, string? orgNumber, string? vatNumber, Address? address, int? paymentTermsDays)
    {
        Name = name.Trim();
        Email = email;
        OrgNumber = orgNumber;
        VatNumber = vatNumber;
        Address = address;
        if (paymentTermsDays is > 0) PaymentTermsDays = paymentTermsDays.Value;
    }

    public void Archive() => Status = CustomerStatus.Archived;
}
