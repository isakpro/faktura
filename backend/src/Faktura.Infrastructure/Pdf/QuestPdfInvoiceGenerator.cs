using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using Faktura.Domain.Organizations;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Faktura.Infrastructure.Pdf;

/// <summary>Renderar en faktura/kreditfaktura till PDF med QuestPDF, inkl. säljarens fakturaprofil.</summary>
internal sealed class QuestPdfInvoiceGenerator : IInvoicePdfGenerator
{
    static QuestPdfInvoiceGenerator()
    {
        // QuestPDF Community-licens (gratis under ~$1M omsättning).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(Invoice invoice, Organization? seller)
    {
        var totals = invoice.Totals;
        var title = invoice.Type == InvoiceType.CreditNote ? "Kreditfaktura" : "Faktura";
        var buyer = invoice.CustomerSnapshot?.Name ?? "";
        var sellerName = seller?.Name ?? "";
        var profile = seller?.Profile;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(sellerName).Bold().FontSize(16);
                        if (profile?.AddressLine is { } line) col.Item().Text(line);
                        if (profile?.PostalCode is not null || profile?.City is not null)
                            col.Item().Text($"{profile?.PostalCode} {profile?.City}".Trim());
                        if (profile?.OrgNumber is { } orgNr) col.Item().Text($"Org.nr {orgNr}");
                    });
                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text(title).Bold().FontSize(16);
                        col.Item().AlignRight().Text($"Nr: {invoice.Number}");
                        col.Item().AlignRight().Text($"Datum: {invoice.InvoiceDate:yyyy-MM-dd}");
                        col.Item().AlignRight().Text($"Förfaller: {invoice.DueDate:yyyy-MM-dd}");
                    });
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Item().Text($"Kund: {buyer}").Bold();
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Beskrivning").Bold();
                            h.Cell().AlignRight().Text("Antal").Bold();
                            h.Cell().AlignRight().Text("À-pris").Bold();
                            h.Cell().AlignRight().Text("Moms%").Bold();
                            h.Cell().AlignRight().Text("Netto").Bold();
                        });

                        foreach (var line in invoice.Lines)
                        {
                            var quantity = line.Unit is null
                                ? line.Quantity.ToString("0.##")
                                : $"{line.Quantity:0.##} {line.Unit}";
                            table.Cell().Text(line.Description);
                            table.Cell().AlignRight().Text(quantity);
                            table.Cell().AlignRight().Text(line.UnitPriceExclVat.ToString("0.00"));
                            table.Cell().AlignRight().Text($"{(int)line.VatRate}%");
                            table.Cell().AlignRight().Text(line.Net.ToString());
                        }
                    });

                    col.Item().PaddingTop(15).AlignRight().Column(sum =>
                    {
                        sum.Item().Text($"Netto: {totals.Net} kr");
                        foreach (var vat in totals.VatByRate)
                            sum.Item().Text($"Moms {vat.RatePercent}%: {vat.Vat} kr");
                        sum.Item().Text($"Att betala: {totals.Gross} kr").Bold().FontSize(12);
                    });
                });

                page.Footer().AlignCenter().Column(footer =>
                {
                    var paymentParts = new List<string>();
                    if (profile?.Bankgiro is { } bg) paymentParts.Add($"Bankgiro {bg}");
                    if (profile?.Plusgiro is { } pg) paymentParts.Add($"Plusgiro {pg}");
                    if (profile?.OrgNumber is { } nr) paymentParts.Add($"Org.nr {nr}");
                    if (profile?.FSkatt == true) paymentParts.Add("Godkänd för F-skatt");

                    if (paymentParts.Count > 0)
                        footer.Item().AlignCenter().Text(string.Join("  ·  ", paymentParts)).FontSize(9);
                    footer.Item().AlignCenter().Text(t =>
                        t.Span("Faktura genererad av Faktura.").FontColor(Colors.Grey.Medium).FontSize(8));
                });
            });
        });

        return document.GeneratePdf();
    }
}
