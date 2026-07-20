using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Api.Features.Webhooks;
using Faktura.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 016: webhook-mottagare (CRUD) och att fakturahändelser triggar dispatch.</summary>
public class WebhookEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public WebhookEndpointsTests(FakturaApiFactory factory) => _factory = factory;

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
    public async Task Owner_can_create_list_and_delete_an_endpoint()
    {
        var a = await OwnerAsync("webhook-owner@acme.se", "WebhookOwner");

        var create = await a.PostAsJsonAsync("/api/webhooks", new CreateWebhookRequest("https://example.com/hooks/faktura"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<CreatedWebhookDto>())!;
        Assert.NotEmpty(created.Secret);

        var list = await a.GetFromJsonAsync<List<WebhookEndpointDto>>("/api/webhooks");
        Assert.Single(list!);
        Assert.DoesNotContain(created.Secret, System.Text.Json.JsonSerializer.Serialize(list)); // hemligheten läcker aldrig i listan

        var delete = await a.DeleteAsync($"/api/webhooks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<List<WebhookEndpointDto>>("/api/webhooks"))!);
    }

    [Fact]
    public async Task Invalid_url_is_rejected()
    {
        var a = await OwnerAsync("webhook-badurl@acme.se", "WebhookBadUrl");
        var resp = await a.PostAsJsonAsync("/api/webhooks", new CreateWebhookRequest("not-a-url"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Sending_paying_and_crediting_dispatch_the_expected_events()
    {
        var a = await OwnerAsync("webhook-dispatch@acme.se", "WebhookDispatch");
        var cust = await a.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));
        var customerId = (await cust.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
        var draft = await a.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest(customerId, [new InvoiceLineInput("X", 1, 1000m, 25)]));
        var id = (await draft.Content.ReadFromJsonAsync<InvoiceDto>())!.Id;

        var dispatcher = _factory.Services.GetRequiredService<InMemoryWebhookDispatcher>();

        await a.PostAsync($"/api/invoices/{id}/send", null);
        await a.PostAsJsonAsync($"/api/invoices/{id}/mark-paid", new MarkPaidRequest(DateOnly.FromDateTime(DateTime.UtcNow)));
        await a.PostAsync($"/api/invoices/{id}/credit", null);

        var types = dispatcher.Dispatched.Select(d => d.EventType).ToList();
        Assert.Contains("invoice.sent", types);
        Assert.Contains("invoice.paid", types);
        Assert.Contains("invoice.credited", types);
    }
}
