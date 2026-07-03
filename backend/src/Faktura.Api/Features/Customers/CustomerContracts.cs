namespace Faktura.Api.Features.Customers;

public sealed record AddressDto(string? Line1, string? Line2, string? PostalCode, string? City, string? Country);

public sealed record CustomerRequest(
    string Name,
    string? Email,
    string? OrgNumber,
    string? VatNumber,
    AddressDto? Address,
    int? PaymentTermsDays);

public sealed record CustomerDto(
    string Id,
    string Name,
    string? Email,
    string? OrgNumber,
    string? VatNumber,
    AddressDto? Address,
    int PaymentTermsDays,
    string Status);
