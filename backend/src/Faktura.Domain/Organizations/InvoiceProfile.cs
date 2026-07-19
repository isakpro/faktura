namespace Faktura.Domain.Organizations;

/// <summary>
/// Organisationens fakturaprofil — säljaruppgifterna som en svensk faktura ska bära
/// (organisationsnummer, adress, betalningsuppgifter, F-skatt). Alla fält valfria;
/// PDF:n renderar det som finns (spec 009).
/// </summary>
public sealed record InvoiceProfile(
    string? OrgNumber,
    string? AddressLine,
    string? PostalCode,
    string? City,
    string? Bankgiro,
    string? Plusgiro,
    bool FSkatt);
