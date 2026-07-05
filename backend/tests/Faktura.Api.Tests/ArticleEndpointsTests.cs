using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Articles;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Xunit;

namespace Faktura.Api.Tests;

public class ArticleEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ArticleEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> CreateArticleAsync(HttpClient c, string name, string? sku = null,
        string? unit = null, decimal price = 100m, int vat = 25)
        => c.PostAsJsonAsync("/api/articles", new ArticleRequest(name, sku, unit, price, vat));

    [Fact]
    public async Task Create_list_update_archive_flow()
    {
        var client = await OwnerAsync("art-1@acme.se", "Art1");

        var created = await CreateArticleAsync(client, "Konsulttimme", "K-100", "tim", 1200m, 25);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var article = (await created.Content.ReadFromJsonAsync<ArticleDto>())!;
        Assert.Equal("tim", article.Unit);

        // Uppdatera pris — registret ändras.
        var updated = await client.PutAsJsonAsync($"/api/articles/{article.Id}",
            new ArticleRequest("Konsulttimme", "K-100", "tim", 1350m, 25));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(1350m, (await updated.Content.ReadFromJsonAsync<ArticleDto>())!.UnitPriceExclVat);

        // Arkivera — försvinner ur aktiva listan men finns i "all".
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/articles/{article.Id}/archive", null)).StatusCode);
        var active = await client.GetFromJsonAsync<List<ArticleDto>>("/api/articles");
        var all = await client.GetFromJsonAsync<List<ArticleDto>>("/api/articles?status=all");
        Assert.DoesNotContain(active!, a => a.Id == article.Id);
        Assert.Contains(all!, a => a.Id == article.Id && a.Status == "Archived");
    }

    [Fact]
    public async Task Duplicate_sku_within_tenant_409_but_ok_across_tenants()
    {
        var a = await OwnerAsync("art-2a@acme.se", "Art2A");
        var b = await OwnerAsync("art-2b@acme.se", "Art2B");

        Assert.Equal(HttpStatusCode.Created, (await CreateArticleAsync(a, "X", "SKU-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CreateArticleAsync(a, "Y", "SKU-1")).StatusCode); // sku_taken
        Assert.Equal(HttpStatusCode.Created, (await CreateArticleAsync(b, "Z", "SKU-1")).StatusCode);  // annan tenant OK

        // Flera artiklar utan sku är OK.
        Assert.Equal(HttpStatusCode.Created, (await CreateArticleAsync(a, "U1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateArticleAsync(a, "U2")).StatusCode);
    }

    [Fact]
    public async Task Articles_are_tenant_isolated()
    {
        var a = await OwnerAsync("art-3a@acme.se", "Art3A");
        var b = await OwnerAsync("art-3b@acme.se", "Art3B");
        var created = (await (await CreateArticleAsync(a, "Hemlig artikel")).Content.ReadFromJsonAsync<ArticleDto>())!;

        var bList = await b.GetFromJsonAsync<List<ArticleDto>>("/api/articles?status=all");
        Assert.Empty(bList!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/articles/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_article_rejected_400()
    {
        var client = await OwnerAsync("art-4@acme.se", "Art4");
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateArticleAsync(client, "", price: 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateArticleAsync(client, "X", price: -1m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateArticleAsync(client, "X", vat: 13)).StatusCode);
    }

    [Fact]
    public async Task Unit_flows_from_line_input_to_dto_and_price_changes_do_not_touch_invoices()
    {
        var client = await OwnerAsync("art-5@acme.se", "Art5");

        // Artikel + kund + utkast med rad "från artikeln" (klienten kopierar värden + unit).
        var article = (await (await CreateArticleAsync(client, "Konsulttimme", "K-1", "tim", 1200m, 25)).Content
            .ReadFromJsonAsync<ArticleDto>())!;
        var customer = (await (await client.PostAsJsonAsync("/api/customers",
            new CustomerRequest("Kund AB", "k@x.se", null, null, null, 30))).Content
            .ReadFromJsonAsync<CustomerDto>())!;

        var draftResp = await client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customer.Id,
            [new InvoiceLineInput(article.Name, 10, article.UnitPriceExclVat, article.VatRate, article.Unit)]));
        var draft = (await draftResp.Content.ReadFromJsonAsync<InvoiceDto>())!;

        Assert.Equal("tim", draft.Lines[0].Unit);
        Assert.Equal(12000m, draft.Totals.Net);

        // Prisändring i registret rör inte utkastet (snapshot-principen).
        await client.PutAsJsonAsync($"/api/articles/{article.Id}",
            new ArticleRequest("Konsulttimme", "K-1", "tim", 9999m, 25));
        var reread = await client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{draft.Id}");
        Assert.Equal(1200m, reread!.Lines[0].UnitPriceExclVat);

        // Skickad faktura med enhet ger giltig PDF.
        await client.PostAsync($"/api/invoices/{draft.Id}/send", null);
        var pdf = await client.GetAsync($"/api/invoices/{draft.Id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("%PDF"u8.ToArray(), (await pdf.Content.ReadAsByteArrayAsync())[..4]);
    }
}
