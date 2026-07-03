using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Xunit;

namespace Faktura.Api.Tests;

public class InvoicingEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public InvoicingEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string> CreateCustomerAsync(HttpClient client, string name = "Kund AB")
    {
        var resp = await client.PostAsJsonAsync("/api/customers",
            new CustomerRequest(name, "k@kund.se", null, null, null, 30));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
    }

    private static async Task<InvoiceDto> CreateDraftAsync(HttpClient client, string customerId, params InvoiceLineInput[] lines)
    {
        var resp = await client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, lines.ToList()));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    [Fact]
    public async Task Customers_are_tenant_isolated()
    {
        var a = await OwnerAsync("inv-a@acme.se", "OrgA");
        var b = await OwnerAsync("inv-b@acme.se", "OrgB");
        await CreateCustomerAsync(a, "A:s kund");

        var aList = await a.GetFromJsonAsync<List<CustomerDto>>("/api/customers");
        var bList = await b.GetFromJsonAsync<List<CustomerDto>>("/api/customers");

        Assert.Single(aList!);
        Assert.Empty(bList!);
    }

    [Fact]
    public async Task Draft_computes_vat_totals_per_rate()
    {
        var a = await OwnerAsync("inv-vat@acme.se", "Vat");
        var customerId = await CreateCustomerAsync(a);

        var draft = await CreateDraftAsync(a, customerId,
            new InvoiceLineInput("Konsult", 1, 1000m, 25),
            new InvoiceLineInput("Bok", 2, 500m, 12));

        Assert.Equal(2000m, draft.Totals.Net);
        Assert.Equal(2370m, draft.Totals.Gross); // 2000 + 250 + 120
        Assert.Equal(250m, draft.Totals.VatByRate.First(v => v.Rate == 25).Vat);
        Assert.Equal(120m, draft.Totals.VatByRate.First(v => v.Rate == 12).Vat);
        Assert.Null(draft.Number); // utkast saknar nummer
        Assert.Equal("Draft", draft.Status);
    }

    [Fact]
    public async Task Send_assigns_sequential_numbers_and_locks()
    {
        var a = await OwnerAsync("inv-send@acme.se", "Send");
        var customerId = await CreateCustomerAsync(a);
        var d1 = await CreateDraftAsync(a, customerId, new InvoiceLineInput("A", 1, 100m, 25));
        var d2 = await CreateDraftAsync(a, customerId, new InvoiceLineInput("B", 1, 100m, 25));

        var s1 = (await (await a.PostAsync($"/api/invoices/{d1.Id}/send", null)).Content.ReadFromJsonAsync<InvoiceDto>())!;
        var s2 = (await (await a.PostAsync($"/api/invoices/{d2.Id}/send", null)).Content.ReadFromJsonAsync<InvoiceDto>())!;

        Assert.Equal(1, s1.Number);
        Assert.Equal(2, s2.Number);
        Assert.Equal("Sent", s1.Status);
        Assert.NotNull(s1.DueDate);

        // Skickad faktura kan inte ändras.
        var edit = await a.PutAsJsonAsync($"/api/invoices/{d1.Id}",
            new UpdateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1m, 0)]));
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode); // invoice_locked
    }

    [Fact]
    public async Task Concurrent_sends_get_unique_unbroken_numbers()
    {
        var a = await OwnerAsync("inv-conc@acme.se", "Conc");
        var customerId = await CreateCustomerAsync(a);

        const int n = 20;
        var ids = new List<string>();
        for (var i = 0; i < n; i++)
            ids.Add((await CreateDraftAsync(a, customerId, new InvoiceLineInput("Rad", 1, 100m, 25))).Id);

        var sends = ids.Select(async id =>
        {
            var resp = await a.PostAsync($"/api/invoices/{id}/send", null);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!.Number!.Value;
        });
        var numbers = await Task.WhenAll(sends);

        Assert.Equal(n, numbers.Distinct().Count());                 // inga dubbletter
        Assert.Equal(Enumerable.Range(1, n).Select(x => (long)x), numbers.OrderBy(x => x)); // obruten 1..n
    }

    [Fact]
    public async Task Mark_paid_sets_status()
    {
        var a = await OwnerAsync("inv-paid@acme.se", "Paid");
        var customerId = await CreateCustomerAsync(a);
        var d = await CreateDraftAsync(a, customerId, new InvoiceLineInput("A", 1, 100m, 25));
        await a.PostAsync($"/api/invoices/{d.Id}/send", null);

        var resp = await a.PostAsJsonAsync($"/api/invoices/{d.Id}/mark-paid", new MarkPaidRequest(new DateOnly(2026, 7, 20)));
        resp.EnsureSuccessStatusCode();
        var paid = (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;
        Assert.Equal("Paid", paid.Status);
    }

    [Fact]
    public async Task Cannot_read_another_tenants_invoice_404()
    {
        var a = await OwnerAsync("inv-xa@acme.se", "XA");
        var b = await OwnerAsync("inv-xb@acme.se", "XB");
        var custA = await CreateCustomerAsync(a);
        var draftA = await CreateDraftAsync(a, custA, new InvoiceLineInput("A", 1, 100m, 25));

        var resp = await b.GetAsync($"/api/invoices/{draftA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Invoices_require_auth_401()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/invoices")).StatusCode);
    }
}
