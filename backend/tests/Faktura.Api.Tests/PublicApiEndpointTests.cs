using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.ApiKeys;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Domain.PublicApi;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 016: det publika, nyckel-autentiserade API:et (/api/v1).</summary>
public class PublicApiEndpointTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public PublicApiEndpointTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string> CreateKeyAsync(HttpClient owner, params string[] scopes)
    {
        var resp = await owner.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest("Test-nyckel", scopes.ToList()));
        return (await resp.Content.ReadFromJsonAsync<CreatedApiKeyDto>())!.Key;
    }

    private HttpClient ApiClient(string rawKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        return client;
    }

    [Fact]
    public async Task Invoices_list_and_get_require_invoices_read_scope()
    {
        var owner = await OwnerAsync("pubapi-inv@acme.se", "PubApiInv");
        var cust = await owner.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await owner.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        await owner.PostAsync($"/api/invoices/{id}/send", null);

        var readKey = await CreateKeyAsync(owner, ApiScopes.InvoicesRead);
        var readClient = ApiClient(readKey);

        var list = await readClient.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/v1/invoices");
        Assert.Single(list!);

        var get = await readClient.GetAsync($"/api/v1/invoices/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // En nyckel utan invoices:read nekas trots giltig nyckel.
        var writeOnlyKey = await CreateKeyAsync(owner, ApiScopes.CustomersWrite);
        var forbidden = await ApiClient(writeOnlyKey).GetAsync("/api/v1/invoices");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Customers_read_and_write_are_scope_gated_and_reuse_the_same_service()
    {
        var owner = await OwnerAsync("pubapi-cust@acme.se", "PubApiCust");
        var readKey = await CreateKeyAsync(owner, ApiScopes.CustomersRead);
        var writeKey = await CreateKeyAsync(owner, ApiScopes.CustomersWrite);

        var createWithReadOnly = await ApiClient(readKey).PostAsJsonAsync("/api/v1/customers",
            new CustomerRequest("Ny Kund AB", null, null, null, null, 30));
        Assert.Equal(HttpStatusCode.Forbidden, createWithReadOnly.StatusCode);

        var created = await ApiClient(writeKey).PostAsJsonAsync("/api/v1/customers",
            new CustomerRequest("Ny Kund AB", null, null, null, null, 30));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await ApiClient(readKey).GetFromJsonAsync<List<CustomerDto>>("/api/v1/customers");
        Assert.Contains(list!, c => c.Name == "Ny Kund AB");

        // Samma kund syns via den vanliga SPA-endpointen — bevisar att det är samma tjänst/data.
        var spaList = await owner.GetFromJsonAsync<List<CustomerDto>>("/api/customers");
        Assert.Contains(spaList!, c => c.Name == "Ny Kund AB");
    }

    [Fact]
    public async Task Unknown_key_is_rejected()
    {
        var resp = await ApiClient("fkt_live_does-not-exist").GetAsync("/api/v1/invoices");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Revoked_key_no_longer_authenticates()
    {
        var owner = await OwnerAsync("pubapi-revoked@acme.se", "PubApiRevoked");
        var createResp = await owner.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest("Kortlivad", [ApiScopes.InvoicesRead]));
        var created = (await createResp.Content.ReadFromJsonAsync<CreatedApiKeyDto>())!;
        await owner.DeleteAsync($"/api/api-keys/{created.Id}");

        var resp = await ApiClient(created.Key).GetAsync("/api/v1/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Api_key_is_tenant_isolated()
    {
        var a = await OwnerAsync("pubapi-iso-a@acme.se", "PubApiIsoA");
        var b = await OwnerAsync("pubapi-iso-b@acme.se", "PubApiIsoB");
        await a.PostAsJsonAsync("/api/customers", new CustomerRequest("A:s kund", null, null, null, null, 30));

        var bKey = await CreateKeyAsync(b, ApiScopes.CustomersRead);
        var list = await ApiClient(bKey).GetFromJsonAsync<List<CustomerDto>>("/api/v1/customers");

        Assert.Empty(list!);
    }
}
