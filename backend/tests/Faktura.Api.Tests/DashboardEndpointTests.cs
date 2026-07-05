using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Xunit;

namespace Faktura.Api.Tests;

public class DashboardEndpointTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public DashboardEndpointTests(FakturaApiFactory factory) => _factory = factory;

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
    public async Task Dashboard_aggregates_only_own_tenants_invoices()
    {
        var a = await OwnerAsync("dash-a@acme.se", "DashA");
        var b = await OwnerAsync("dash-b@acme.se", "DashB");

        // A: en skickad (1250 brutto) + en betald idag (1250).
        var custA = (await (await a.PostAsJsonAsync("/api/customers",
            new CustomerRequest("KA", null, null, null, null, 30))).Content.ReadFromJsonAsync<CustomerDto>())!;
        foreach (var pay in new[] { false, true })
        {
            var draft = (await (await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(custA.Id,
                [new InvoiceLineInput("Rad", 1, 1000m, 25)]))).Content.ReadFromJsonAsync<InvoiceDto>())!;
            await a.PostAsync($"/api/invoices/{draft.Id}/send", null);
            if (pay)
                await a.PostAsJsonAsync($"/api/invoices/{draft.Id}/mark-paid",
                    new MarkPaidRequest(DateOnly.FromDateTime(DateTime.UtcNow)));
        }

        var dashA = await a.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        Assert.Equal(1250m, dashA!.Outstanding);
        Assert.Equal(0m, dashA.Overdue);
        Assert.Equal(1250m, dashA.PaidThisYear);
        Assert.Equal(12, dashA.MonthlyRevenue.Count);
        Assert.Equal(1250m, dashA.MonthlyRevenue[^1].Gross); // betalningen ligger i senaste månaden
        Assert.Equal(2, dashA.RecentInvoices.Count);

        // B är opåverkad av A:s fakturor.
        var dashB = await b.GetFromJsonAsync<DashboardDto>("/api/dashboard");
        Assert.Equal(0m, dashB!.Outstanding);
        Assert.Empty(dashB.RecentInvoices);
    }

    [Fact]
    public async Task Dashboard_requires_auth_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().GetAsync("/api/dashboard")).StatusCode);
    }
}
