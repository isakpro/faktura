using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 013: kundportal via kapabilitets-token, utan autentisering.</summary>
public class PublicPortalTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public PublicPortalTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<InvoiceDto> SentInvoiceAsync(HttpClient client)
    {
        var cust = await client.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", "k@kund.se", null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId,
            [new InvoiceLineInput("Konsult", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        var sent = await client.PostAsync($"/api/invoices/{id}/send", null);
        return (await sent.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    [Fact]
    public async Task Share_is_idempotent_and_returns_stable_url()
    {
        var a = await OwnerAsync("portal-share@acme.se", "Share");
        var invoice = await SentInvoiceAsync(a);

        var first = await (await a.PostAsync($"/api/invoices/{invoice.Id}/share", null)).Content.ReadFromJsonAsync<ShareLinkDto>();
        var second = await (await a.PostAsync($"/api/invoices/{invoice.Id}/share", null)).Content.ReadFromJsonAsync<ShareLinkDto>();

        Assert.Equal(first!.Url, second!.Url);
        Assert.Contains($"/f/{first.Token}", first.Url);
        Assert.Equal(32, first.Token.Length);
    }

    [Fact]
    public async Task Draft_cannot_be_shared()
    {
        var a = await OwnerAsync("portal-draft@acme.se", "Draft");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("K", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1m, 0)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;

        var resp = await a.PostAsync($"/api/invoices/{id}/share", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Share_is_tenant_isolated()
    {
        var a = await OwnerAsync("portal-iso-a@acme.se", "IsoA");
        var b = await OwnerAsync("portal-iso-b@acme.se", "IsoB");
        var invoice = await SentInvoiceAsync(a);

        var resp = await b.PostAsync($"/api/invoices/{invoice.Id}/share", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Public_view_works_without_auth_and_leaks_no_ids()
    {
        var a = await OwnerAsync("portal-pub@acme.se", "Pub");
        var invoice = await SentInvoiceAsync(a);
        await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RegisterPaymentRequest(250m));
        var link = await (await a.PostAsync($"/api/invoices/{invoice.Id}/share", null)).Content.ReadFromJsonAsync<ShareLinkDto>();

        var anon = _factory.CreateClient(); // ingen Authorization-header
        var resp = await anon.GetAsync($"/api/public/invoices/{link!.Token}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = (await resp.Content.ReadFromJsonAsync<PublicInvoiceDto>())!;
        Assert.Equal("PartiallyPaid", dto.Status);
        Assert.Equal(1000m, dto.RemainingAmount);
        Assert.Equal("Kund AB", dto.CustomerName);
        Assert.Equal("Pub", dto.Seller.Name);
        Assert.NotNull(dto.OcrNumber);

        // Kapabilitets-svaret får inte läcka interna id:n.
        var json = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain(invoice.Id, json);
        Assert.DoesNotContain("tenantId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_token_gives_404()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/public/invoices/{new string('a', 32)}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Public_pdf_downloads_without_auth()
    {
        var a = await OwnerAsync("portal-pdf@acme.se", "Pdf");
        var invoice = await SentInvoiceAsync(a);
        var link = await (await a.PostAsync($"/api/invoices/{invoice.Id}/share", null)).Content.ReadFromJsonAsync<ShareLinkDto>();

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/public/invoices/{link!.Token}/pdf");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/pdf", resp.Content.Headers.ContentType?.MediaType);
        Assert.True((await resp.Content.ReadAsByteArrayAsync()).Length > 500);
    }

    [Fact]
    public async Task Emailed_invoice_contains_portal_link()
    {
        var a = await OwnerAsync("portal-mail@acme.se", "Mail");
        var invoice = await SentInvoiceAsync(a);

        var resp = await a.PostAsJsonAsync($"/api/invoices/{invoice.Id}/email", new SendEmailRequest(null));
        resp.EnsureSuccessStatusCode();

        var sender = _factory.Services.GetRequiredService<Fakes.FakeEmailSender>();
        Assert.Contains("/f/", sender.LastMessage!.Body);
    }
}
