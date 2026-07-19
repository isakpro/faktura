using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Api.Features.Members;
using Xunit;

namespace Faktura.Api.Tests;

public class ProfileEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ProfileEndpointsTests(FakturaApiFactory factory) => _factory = factory;

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
    public async Task Profile_roundtrip_is_tenant_isolated_and_member_cannot_write()
    {
        var a = await OwnerAsync("prof-a@acme.se", "ProfA");
        var b = await OwnerAsync("prof-b@acme.se", "ProfB");

        var put = await a.PutAsJsonAsync("/api/organization-profile",
            new InvoiceProfileDto("556677-8899", "Storgatan 1", "111 22", "Stockholm", "123-4567", null, true));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var readBack = (await a.GetFromJsonAsync<InvoiceProfileDto>("/api/organization-profile"))!;
        Assert.Equal("556677-8899", readBack.OrgNumber);
        Assert.True(readBack.FSkatt);

        // B ser sin egen (tomma) profil — inte A:s.
        var bProfile = (await b.GetFromJsonAsync<InvoiceProfileDto>("/api/organization-profile"))!;
        Assert.Null(bProfile.OrgNumber);

        // Member nekas skriva.
        var invite = await a.PostAsJsonAsync("/api/invitations", new InviteRequest("prof-m@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<InviteResponse>())!.Token;
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PutAsJsonAsync("/api/organization-profile",
            new InvoiceProfileDto(null, null, null, null, null, null, false))).StatusCode);
    }

    [Fact]
    public async Task Pdf_generates_with_and_without_profile()
    {
        var client = await OwnerAsync("prof-pdf@acme.se", "ProfPdf");
        var customer = (await (await client.PostAsJsonAsync("/api/customers",
            new CustomerRequest("Kund AB", null, null, null, null, 30))).Content.ReadFromJsonAsync<CustomerDto>())!;

        async Task<byte[]> SentPdfAsync()
        {
            var draft = (await (await client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customer.Id,
                [new InvoiceLineInput("Rad", 1, 100m, 25)]))).Content.ReadFromJsonAsync<InvoiceDto>())!;
            await client.PostAsync($"/api/invoices/{draft.Id}/send", null);
            var pdf = await client.GetAsync($"/api/invoices/{draft.Id}/pdf");
            pdf.EnsureSuccessStatusCode();
            return await pdf.Content.ReadAsByteArrayAsync();
        }

        var withoutProfile = await SentPdfAsync();
        Assert.Equal("%PDF"u8.ToArray(), withoutProfile[..4]);

        await client.PutAsJsonAsync("/api/organization-profile",
            new InvoiceProfileDto("556677-8899", "Storgatan 1", "111 22", "Stockholm", "123-4567", "98 76 54-3", true));
        var withProfile = await SentPdfAsync();
        Assert.Equal("%PDF"u8.ToArray(), withProfile[..4]);
        Assert.NotEqual(withoutProfile.Length, withProfile.Length); // profilen renderas
    }
}
