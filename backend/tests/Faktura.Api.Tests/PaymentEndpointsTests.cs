using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 012: OCR-nummer, betalningsreskontra och delkreditering.</summary>
public class PaymentEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public PaymentEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<InvoiceDto> SentInvoiceAsync(HttpClient client, params InvoiceLineInput[] lines)
    {
        var cust = await client.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", "k@kund.se", null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId,
            lines.Length > 0 ? lines.ToList() : [new InvoiceLineInput("Konsult", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        var sent = await client.PostAsync($"/api/invoices/{id}/send", null);
        return (await sent.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    [Fact]
    public async Task Sent_invoice_gets_valid_ocr_number()
    {
        var a = await OwnerAsync("pay-ocr@acme.se", "Ocr");
        var invoice = await SentInvoiceAsync(a);

        Assert.NotNull(invoice.OcrNumber);
        Assert.True(Faktura.Domain.Invoicing.OcrReference.IsValid(invoice.OcrNumber!));
        Assert.Equal(1250m, invoice.RemainingAmount);
    }

    [Fact]
    public async Task Partial_payment_yields_partially_paid_and_final_payment_paid()
    {
        var a = await OwnerAsync("pay-partial@acme.se", "Partial");
        var invoice = await SentInvoiceAsync(a); // brutto 1250

        var p1 = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments",
            new RegisterPaymentRequest(500m, new DateOnly(2026, 7, 10), "Bankgiro"));
        var afterP1 = (await p1.Content.ReadFromJsonAsync<InvoiceDto>())!;
        Assert.Equal("PartiallyPaid", afterP1.Status);
        Assert.Equal(750m, afterP1.RemainingAmount);

        var p2 = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments",
            new RegisterPaymentRequest(750m, new DateOnly(2026, 7, 15), null));
        var afterP2 = (await p2.Content.ReadFromJsonAsync<InvoiceDto>())!;
        Assert.Equal("Paid", afterP2.Status);
        Assert.Equal(0m, afterP2.RemainingAmount);
        Assert.Equal(new DateOnly(2026, 7, 15), afterP2.PaidDate);

        var history = await a.GetFromJsonAsync<List<PaymentDto>>($"/api/invoices/{invoice.Id}/payments");
        Assert.Equal(2, history!.Count);
        Assert.Equal("Bankgiro", history.Single(p => p.Amount == 500m).Note);
    }

    [Fact]
    public async Task Overpayment_is_rejected_with_400()
    {
        var a = await OwnerAsync("pay-over@acme.se", "Over");
        var invoice = await SentInvoiceAsync(a); // brutto 1250

        var resp = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments",
            new RegisterPaymentRequest(1300m));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Mark_paid_registers_remaining_as_payment()
    {
        var a = await OwnerAsync("pay-mark@acme.se", "Mark");
        var invoice = await SentInvoiceAsync(a); // brutto 1250
        await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RegisterPaymentRequest(250m));

        var resp = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/mark-paid",
            new MarkPaidRequest(new DateOnly(2026, 7, 18)));
        var paid = (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;

        Assert.Equal("Paid", paid.Status);
        var history = await a.GetFromJsonAsync<List<PaymentDto>>($"/api/invoices/{invoice.Id}/payments");
        Assert.Equal(2, history!.Count);
        Assert.Contains(history, p => p.Amount == 1000m); // resterande saldo
    }

    [Fact]
    public async Task Payments_are_tenant_isolated()
    {
        var a = await OwnerAsync("pay-iso-a@acme.se", "IsoA");
        var b = await OwnerAsync("pay-iso-b@acme.se", "IsoB");
        var invoice = await SentInvoiceAsync(a);

        var resp = await b.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RegisterPaymentRequest(100m));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Partial_credit_creates_note_with_selected_lines_only()
    {
        var a = await OwnerAsync("pay-credit@acme.se", "Credit");
        var invoice = await SentInvoiceAsync(a,
            new InvoiceLineInput("Konsult", 10, 1000m, 25),
            new InvoiceLineInput("Resa", 4, 500m, 6));

        var resp = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/credit",
            new CreditRequest([new CreditLineInput(0, 2)]));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var note = (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;

        Assert.Equal("CreditNote", note.Type);
        var line = Assert.Single(note.Lines);
        Assert.Equal(-2m, line.Quantity);
        Assert.Equal(-2500m, note.Totals.Gross);

        // Originalet är inte fullkrediterat — mer kan fortfarande krediteras.
        var original = await a.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}");
        Assert.NotEqual("Credited", original!.Status);
    }

    [Fact]
    public async Task Credit_without_body_still_credits_everything()
    {
        var a = await OwnerAsync("pay-fullcredit@acme.se", "FullCredit");
        var invoice = await SentInvoiceAsync(a); // brutto 1250

        var resp = await a.PostAsync($"/api/invoices/{invoice.Id}/credit", null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var original = await a.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoice.Id}");
        Assert.Equal("Credited", original!.Status);
    }

    [Fact]
    public async Task Invalid_credit_selection_does_not_consume_invoice_number()
    {
        var a = await OwnerAsync("pay-badcredit@acme.se", "BadCredit");
        var invoice = await SentInvoiceAsync(a); // nummer 1

        var bad = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/credit",
            new CreditRequest([new CreditLineInput(5, 1)]));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Nästa skickade faktura ska få nummer 2 — serien har inga hopp.
        var next = await SentInvoiceAsync(a);
        Assert.Equal(2, next.Number);
    }
}
