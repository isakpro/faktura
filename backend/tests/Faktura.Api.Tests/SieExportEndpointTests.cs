using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Api.Features.Members;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 015: SIE4-export för bokföring.</summary>
public class SieExportEndpointTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public SieExportEndpointTests(FakturaApiFactory factory) => _factory = factory;

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
    public async Task Owner_can_download_sie_file_for_year()
    {
        var a = await OwnerAsync("sie-owner@acme.se", "SieOwner");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        await a.PostAsync($"/api/invoices/{id}/send", null);

        var resp = await a.GetAsync("/api/export/sie?year=2026");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/octet-stream", resp.Content.Headers.ContentType?.MediaType);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("#SIETYP 4", text);
        Assert.Contains("#VER \"F\" \"1\"", text);
    }

    [Fact]
    public async Task Member_cannot_export()
    {
        var owner = await OwnerAsync("sie-member-owner@acme.se", "SieMember");
        var invite = await owner.PostAsJsonAsync("/api/invitations", new InviteRequest("sie-member@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<InviteResponse>())!.Token;
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var resp = await memberClient.GetAsync("/api/export/sie?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Export_is_tenant_isolated()
    {
        var a = await OwnerAsync("sie-iso-a@acme.se", "SieIsoA");
        var b = await OwnerAsync("sie-iso-b@acme.se", "SieIsoB");
        var custA = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund A", null, null, null, null, 30));
        var customerIdA = (await custA.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draftA = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerIdA, [new InvoiceLineInput("X", 1, 1000m, 25)]));
        var idA = (await draftA.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;
        await a.PostAsync($"/api/invoices/{idA}/send", null);

        var respB = await b.GetAsync("/api/export/sie?year=2026");
        var textB = System.Text.Encoding.Latin1.GetString(await respB.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        Assert.DoesNotContain("#VER", textB); // b:s tenant har inga fakturor alls
    }
}
