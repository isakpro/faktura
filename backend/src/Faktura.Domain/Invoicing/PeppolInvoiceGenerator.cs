using System.Xml.Linq;
using Faktura.Domain.Organizations;

namespace Faktura.Domain.Invoicing;

/// <summary>
/// Genererar ett UBL 2.1-dokument enligt Peppol BIS Billing 3.0 (EN 16931-profilen) för en
/// skickad faktura. Rent XML-bygge — ingen I/O; anropande lager avgör filnamn/content-type.
/// Fakturor exporteras som ett Invoice-dokument, kreditfakturor som CreditNote (spec 014).
/// </summary>
public static class PeppolInvoiceGenerator
{
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace CreditNoteNs = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    private const string CustomizationId = "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0";
    private const string ProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";
    private const string Currency = "SEK";

    public static XDocument Generate(Invoice invoice, Organization? seller)
    {
        var isCredit = invoice.Type == InvoiceType.CreditNote;
        var ns = isCredit ? CreditNoteNs : InvoiceNs;
        var rootName = isCredit ? "CreditNote" : "Invoice";
        var lineName = isCredit ? "CreditNoteLine" : "InvoiceLine";
        var quantityElement = isCredit ? "CreditedQuantity" : "InvoicedQuantity";
        var totals = invoice.Totals;

        var root = new XElement(ns + rootName,
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XElement(Cbc + "CustomizationID", CustomizationId),
            new XElement(Cbc + "ProfileID", ProfileId),
            new XElement(Cbc + "ID", invoice.Number),
            new XElement(Cbc + "IssueDate", invoice.InvoiceDate?.ToString("yyyy-MM-dd")),
            isCredit ? null : new XElement(Cbc + "DueDate", invoice.DueDate?.ToString("yyyy-MM-dd")),
            new XElement(Cbc + (isCredit ? "CreditNoteTypeCode" : "InvoiceTypeCode"), isCredit ? "381" : "380"),
            new XElement(Cbc + "DocumentCurrencyCode", Currency),
            isCredit && invoice.OriginalInvoiceId is not null
                ? new XElement(Cac + "BillingReference",
                    new XElement(Cac + "InvoiceDocumentReference", new XElement(Cbc + "ID", invoice.OriginalInvoiceId)))
                : null,
            SupplierParty(seller),
            CustomerParty(invoice.CustomerSnapshot),
            TaxTotal(totals),
            MonetaryTotal(totals),
            invoice.Lines.Select((line, i) => Line(ns, lineName, quantityElement, i + 1, line)));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement SupplierParty(Organization? seller)
    {
        var profile = seller?.Profile;
        return new XElement(Cac + "AccountingSupplierParty",
            new XElement(Cac + "Party",
                profile?.OrgNumber is { } orgNr
                    ? new XElement(Cac + "PartyLegalEntity", new XElement(Cbc + "CompanyID", orgNr))
                    : null,
                new XElement(Cac + "PartyName", new XElement(Cbc + "Name", seller?.Name ?? "")),
                Address(profile?.AddressLine, profile?.PostalCode, profile?.City)));
    }

    private static XElement CustomerParty(CustomerSnapshot? customer) =>
        new(Cac + "AccountingCustomerParty",
            new XElement(Cac + "Party",
                customer?.VatNumber is { } vat
                    ? new XElement(Cac + "PartyTaxScheme",
                        new XElement(Cbc + "CompanyID", vat),
                        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))
                    : null,
                customer?.OrgNumber is { } orgNr
                    ? new XElement(Cac + "PartyLegalEntity", new XElement(Cbc + "CompanyID", orgNr))
                    : null,
                new XElement(Cac + "PartyName", new XElement(Cbc + "Name", customer?.Name ?? "")),
                Address(customer?.Address?.Line1, customer?.Address?.PostalCode, customer?.Address?.City)));

    private static XElement? Address(string? line, string? postalCode, string? city) =>
        line is null && postalCode is null && city is null
            ? null
            : new XElement(Cac + "PostalAddress",
                line is null ? null : new XElement(Cbc + "StreetName", line),
                city is null ? null : new XElement(Cbc + "CityName", city),
                postalCode is null ? null : new XElement(Cbc + "PostalZone", postalCode));

    private static XElement TaxTotal(InvoiceTotals totals) =>
        new(Cac + "TaxTotal",
            AmountElement(Cbc + "TaxAmount", totals.VatByRate.Sum(v => v.Vat.Amount)),
            totals.VatByRate.Select(v =>
                new XElement(Cac + "TaxSubtotal",
                    AmountElement(Cbc + "TaxableAmount", TaxableAmountFor(v)),
                    AmountElement(Cbc + "TaxAmount", v.Vat.Amount),
                    new XElement(Cac + "TaxCategory",
                        new XElement(Cbc + "ID", v.RatePercent == 0 ? "Z" : "S"),
                        new XElement(Cbc + "Percent", v.RatePercent),
                        new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT"))))));

    // Nettobeloppet för en momssats härleds tillbaka ur momsbeloppet — TaxByRate bär bara moms, inte netto.
    private static decimal TaxableAmountFor(VatByRate v) =>
        v.RatePercent == 0 ? 0m : Math.Round(v.Vat.Amount / (v.RatePercent / 100m), 2);

    private static XElement MonetaryTotal(InvoiceTotals totals) =>
        new(Cac + "LegalMonetaryTotal",
            AmountElement(Cbc + "LineExtensionAmount", totals.Net.Amount),
            AmountElement(Cbc + "TaxExclusiveAmount", totals.Net.Amount),
            AmountElement(Cbc + "TaxInclusiveAmount", totals.Gross.Amount),
            AmountElement(Cbc + "PayableAmount", totals.Gross.Amount));

    private static XElement Line(XNamespace ns, string lineName, string quantityElement, int index, InvoiceLine line) =>
        new(ns + lineName,
            new XElement(Cbc + "ID", index),
            new XElement(Cbc + quantityElement, Math.Abs(line.Quantity), new XAttribute("unitCode", UnitCode(line.Unit))),
            AmountElement(Cbc + "LineExtensionAmount", Math.Abs(line.Net.Amount)),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Name", line.Description),
                new XElement(Cac + "ClassifiedTaxCategory",
                    new XElement(Cbc + "ID", line.VatRate == VatRate.Zero ? "Z" : "S"),
                    new XElement(Cbc + "Percent", (int)line.VatRate),
                    new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))),
            new XElement(Cac + "Price", AmountElement(Cbc + "PriceAmount", line.UnitPriceExclVat)));

    // UN/ECE Rec 20-koder: styck/timme; okänd enhet faller tillbaka på "styck" (giltig, konservativ default).
    private static string UnitCode(string? unit) => unit?.ToLowerInvariant() switch
    {
        "tim" or "h" or "timme" or "timmar" => "HUR",
        "kg" => "KGM",
        _ => "H87"
    };

    // UBL kräver ett currencyID-attribut på varje monetärt element.
    private static XElement AmountElement(XName name, decimal value) =>
        new(name, new XAttribute("currencyID", Currency),
            value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
}
