using Faktura.Domain.Abstractions;
using Faktura.Domain.Invoicing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Faktura.Infrastructure.Pdf;

/// <summary>Renderar en faktura/kreditfaktura till PDF med QuestPDF.</summary>
internal sealed class QuestPdfInvoiceGenerator : IInvoicePdfGenerator
{
    static QuestPdfInvoiceGenerator()
    {
        // QuestPDF Community-licens (gratis under ~$1M omsättning).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(Invoice invoice, string sellerName)
    {
        var totals = invoice.Totals;
        var title = invoice.Type == InvoiceType.CreditNote ? "Kreditfaktura" : "Faktura";
        var buyer = invoice.CustomerSnapshot?.Name ?? "";

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

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Faktura genererad av Faktura.").FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }
}
