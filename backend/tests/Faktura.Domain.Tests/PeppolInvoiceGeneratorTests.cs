using System.Xml.Linq;
using Faktura.Domain.Customers;
using Faktura.Domain.Invoicing;
using Faktura.Domain.Organizations;
using Xunit;

namespace Faktura.Domain.Tests;

public class PeppolInvoiceGeneratorTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 7, 20);
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    private static Organization Seller()
    {
        var org = Organization.CreateNew("o-1", "Ramstedt Konsult AB", freeSeatLimit: 2, Now);
        org.UpdateProfile(new InvoiceProfile("556677-8899", "Storgatan 12", "111 22", "Stockholm", "123-4567", null, true));
        return org;
    }

    private static Invoice SentInvoiceWithTwoRates()
    {
        var address = new Address("Kundgatan 1", null, "222 33", "Göteborg", "SE");
        var inv = Invoice.CreateDraft("i-1", "t-1", "c-1",
            [new InvoiceLine("Konsult", 10, 1000m, VatRate.TwentyFive, "tim"),
             new InvoiceLine("Litteratur", 2, 500m, VatRate.Six)], Now);
        var snapshot = new CustomerSnapshot("Nordiska Byggkompaniet AB", "kund@nordbygg.se", "556000-1111", "SE556000111101", address);
        inv.Send(1, Today, snapshot, 30, Now);
        return inv;
    }

    [Fact]
    public void Generate_produces_well_formed_bis3_invoice()
    {
        var doc = PeppolInvoiceGenerator.Generate(SentInvoiceWithTwoRates(), Seller());

        Assert.Equal("Invoice", doc.Root!.Name.LocalName);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", doc.Root.Name.NamespaceName);
        Assert.Equal("urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0",
            doc.Root.Element(Cbc + "CustomizationID")!.Value);
        Assert.Equal("urn:fdc:peppol.eu:2017:poacc:billing:01:1.0", doc.Root.Element(Cbc + "ProfileID")!.Value);
        Assert.Equal("1", doc.Root.Element(Cbc + "ID")!.Value);
        Assert.Equal("380", doc.Root.Element(Cbc + "InvoiceTypeCode")!.Value);
        Assert.Equal("SEK", doc.Root.Element(Cbc + "DocumentCurrencyCode")!.Value);
    }

    [Fact]
    public void Amounts_match_invoice_calculator_across_multiple_vat_rates()
    {
        var invoice = SentInvoiceWithTwoRates(); // 10×1000@25% + 2×500@6% => net 11000, vat 2560, gross 13560
        var doc = PeppolInvoiceGenerator.Generate(invoice, Seller());

        var total = doc.Root!.Element(Cac + "LegalMonetaryTotal")!;
        Assert.Equal("11000.00", total.Element(Cbc + "TaxExclusiveAmount")!.Value);
        Assert.Equal("13560.00", total.Element(Cbc + "TaxInclusiveAmount")!.Value);
        Assert.Equal("13560.00", total.Element(Cbc + "PayableAmount")!.Value);

        var taxTotal = doc.Root.Element(Cac + "TaxTotal")!;
        Assert.Equal("2560.00", taxTotal.Element(Cbc + "TaxAmount")!.Value);
        var subtotals = taxTotal.Elements(Cac + "TaxSubtotal").ToList();
        Assert.Equal(2, subtotals.Count);
        Assert.Contains(subtotals, s => s.Element(Cac + "TaxCategory")!.Element(Cbc + "Percent")!.Value == "25"
            && s.Element(Cbc + "TaxAmount")!.Value == "2500.00");
        Assert.Contains(subtotals, s => s.Element(Cac + "TaxCategory")!.Element(Cbc + "Percent")!.Value == "6"
            && s.Element(Cbc + "TaxAmount")!.Value == "60.00");

        var lines = doc.Root.Elements(doc.Root.Name.Namespace + "InvoiceLine").ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal("HUR", lines[0].Element(Cbc + "InvoicedQuantity")!.Attribute("unitCode")!.Value);
    }

    [Fact]
    public void Seller_and_buyer_parties_are_populated_from_profile_and_snapshot()
    {
        var doc = PeppolInvoiceGenerator.Generate(SentInvoiceWithTwoRates(), Seller());

        var supplier = doc.Root!.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!;
        Assert.Equal("Ramstedt Konsult AB", supplier.Element(Cac + "PartyName")!.Element(Cbc + "Name")!.Value);
        Assert.Equal("556677-8899", supplier.Element(Cac + "PartyLegalEntity")!.Element(Cbc + "CompanyID")!.Value);

        var customer = doc.Root.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!;
        Assert.Equal("Nordiska Byggkompaniet AB", customer.Element(Cac + "PartyName")!.Element(Cbc + "Name")!.Value);
        Assert.Equal("SE556000111101", customer.Element(Cac + "PartyTaxScheme")!.Element(Cbc + "CompanyID")!.Value);
        Assert.Equal("Göteborg", customer.Element(Cac + "PostalAddress")!.Element(Cbc + "CityName")!.Value);
    }

    [Fact]
    public void Missing_buyer_details_are_simply_omitted_not_faked()
    {
        var inv = Invoice.CreateDraft("i-2", "t-1", "c-1", [new InvoiceLine("X", 1, 100m, VatRate.Zero)], Now);
        var snapshot = new CustomerSnapshot("Minimal Kund", null, null, null, null);
        inv.Send(2, Today, snapshot, 30, Now);

        var doc = PeppolInvoiceGenerator.Generate(inv, Seller());

        var customer = doc.Root!.Element(Cac + "AccountingCustomerParty")!.Element(Cac + "Party")!;
        Assert.Null(customer.Element(Cac + "PartyTaxScheme"));
        Assert.Null(customer.Element(Cac + "PartyLegalEntity"));
        Assert.Null(customer.Element(Cac + "PostalAddress"));
    }

    [Fact]
    public void Credit_note_uses_creditnote_root_and_references_original()
    {
        var original = SentInvoiceWithTwoRates();
        var creditLines = original.BuildCreditLines(null).Value;
        var creditNote = Invoice.CreateCreditNote("cn-1", original, 2, Today, Now, creditLines);

        var doc = PeppolInvoiceGenerator.Generate(creditNote, Seller());

        Assert.Equal("CreditNote", doc.Root!.Name.LocalName);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", doc.Root.Name.NamespaceName);
        Assert.Equal("381", doc.Root.Element(Cbc + "CreditNoteTypeCode")!.Value);
        Assert.Equal("i-1", doc.Root.Element(Cac + "BillingReference")!
            .Element(Cac + "InvoiceDocumentReference")!.Element(Cbc + "ID")!.Value);
        var creditLines2 = doc.Root.Elements(doc.Root.Name.Namespace + "CreditNoteLine").ToList();
        Assert.NotEmpty(creditLines2);
        // Negerade kreditrader ska ändå skrivas som positiva mängder/belopp i UBL.
        Assert.Equal("10", creditLines2.First().Element(Cbc + "CreditedQuantity")!.Value);
    }
}
