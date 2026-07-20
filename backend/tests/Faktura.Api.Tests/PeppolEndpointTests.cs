using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 014: Peppol BIS Billing 3.0 (UBL) export.</summary>
public class PeppolEndpointTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public PeppolEndpointTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    [Fact]
    public async Task Sent_invoice_returns_valid_ubl_xml()
    {
        var a = await OwnerAsync("peppol-ok@acme.se", "Peppol");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", "k@kund.se", null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("Konsult", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        await a.PostAsync($"/api/invoices/{id}/send", null);

        var resp = await a.GetAsync($"/api/invoices/{id}/peppol");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/xml", resp.Content.Headers.ContentType?.MediaType);
        var xml = await resp.Content.ReadAsStringAsync();
        Assert.Contains("urn:fdc:peppol.eu:2017:poacc:billing:3.0", xml);
        Assert.Contains("<Invoice", xml);
    }

    [Fact]
    public async Task Draft_cannot_be_exported()
    {
        var a = await OwnerAsync("peppol-draft@acme.se", "Draft");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("K", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1m, 0)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;

        var resp = await a.GetAsync($"/api/invoices/{id}/peppol");

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Export_is_tenant_isolated()
    {
        var a = await OwnerAsync("peppol-iso-a@acme.se", "IsoA");
        var b = await OwnerAsync("peppol-iso-b@acme.se", "IsoB");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1m, 0)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        await a.PostAsync($"/api/invoices/{id}/send", null);

        var resp = await b.GetAsync($"/api/invoices/{id}/peppol");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
