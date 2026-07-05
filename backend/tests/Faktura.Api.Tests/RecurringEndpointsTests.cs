using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Invoicing;
using Faktura.Api.Tests.Fakes;
using Faktura.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Faktura.Api.Tests;

public class RecurringEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public RecurringEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private MutableClock Clock => _factory.Services.GetRequiredService<MutableClock>();
    private FakeEmailSender Sender => (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    private async Task<HttpClient> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string> CustomerAsync(HttpClient client, string? email = "kund@x.se")
    {
        var resp = await client.PostAsJsonAsync("/api/customers",
            new CustomerRequest("Kund AB", email, null, null, null, 30));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
    }

    private DateOnly Today => DateOnly.FromDateTime(Clock.UtcNow);

    private async Task<RecurringInvoiceDto> TemplateAsync(HttpClient client, string customerId,
        string interval = "monthly", DateOnly? start = null, DateOnly? end = null)
    {
        var resp = await client.PostAsJsonAsync("/api/recurring-invoices",
            new RecurringInvoiceRequest(customerId,
                [new InvoiceLineInput("Abonnemang", 1, 500m, 25)], interval, start ?? Today, end));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RecurringInvoiceDto>())!;
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RecurringInvoiceJob>().RunOnceAsync();
    }

    [Fact]
    public async Task Create_validates_and_lists_tenant_isolated()
    {
        var a = await OwnerAsync("rec-1a@acme.se", "Rec1A");
        var b = await OwnerAsync("rec-1b@acme.se", "Rec1B");
        var custA = await CustomerAsync(a);

        var dto = await TemplateAsync(a, custA, "quarterly");
        Assert.Equal("Quarterly", dto.Interval);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(625m, dto.Gross); // 500 + 25 % moms

        // Ogiltigt intervall + okänd kund valideras.
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/recurring-invoices",
            new RecurringInvoiceRequest(custA, [new InvoiceLineInput("X", 1, 1m, 25)], "weekly", Today, null))).StatusCode);

        // B ser inte A:s mallar.
        Assert.Empty((await b.GetFromJsonAsync<List<RecurringInvoiceDto>>("/api/recurring-invoices"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/recurring-invoices/{dto.Id}/pause", null)).StatusCode);
    }

    [Fact]
    public async Task Job_generates_sends_and_emails_then_advances()
    {
        var client = await OwnerAsync("rec-2@acme.se", "Rec2");
        var customerId = await CustomerAsync(client);
        var template = await TemplateAsync(client, customerId); // start = idag -> förfallen direkt

        var generated = await RunJobAsync();
        Assert.True(generated >= 1);

        // Fakturan är skickad (nummer + låst) och mejlad med PDF.
        var invoices = await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices");
        var inv = invoices!.Single();
        Assert.NotNull(inv.Number);
        Assert.Equal("Sent", inv.Status);
        Assert.Equal(625m, inv.Gross);
        Assert.Equal("kund@x.se", Sender.LastMessage!.To);
        Assert.NotNull(Sender.LastMessage.Attachment);
        Assert.Null(Sender.LastMessage.ReplyTo); // automatiskt utskick

        // Spårbarhet + framflyttad nästa körning; omkörning ger ingen dubblett.
        var linked = await client.GetFromJsonAsync<List<InvoiceListItemDto>>($"/api/recurring-invoices/{template.Id}/generated");
        Assert.Single(linked!);
        var reread = (await client.GetFromJsonAsync<List<RecurringInvoiceDto>>("/api/recurring-invoices"))!.Single();
        Assert.Equal(template.NextRunDate.AddMonths(1), reread.NextRunDate);

        await RunJobAsync();
        Assert.Single((await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!);
    }

    [Fact]
    public async Task Job_catches_up_missed_periods_with_sequential_numbers()
    {
        var client = await OwnerAsync("rec-3@acme.se", "Rec3");
        var customerId = await CustomerAsync(client);
        await TemplateAsync(client, customerId); // månadsvis, start = idag

        Clock.Advance(TimeSpan.FromDays(65)); // ~2 månader förflutna -> 3 förfallna perioder (start, +1M, +2M)
        await RunJobAsync();

        var invoices = (await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!;
        Assert.Equal(3, invoices.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, invoices.Select(i => i.Number!.Value).OrderBy(n => n).ToArray());

        // Omkörning: inga fler.
        await RunJobAsync();
        Assert.Equal(3, (await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!.Count);
    }

    [Fact]
    public async Task Paused_template_does_not_generate_until_resumed()
    {
        var client = await OwnerAsync("rec-4@acme.se", "Rec4");
        var customerId = await CustomerAsync(client);

        var paused = await TemplateAsync(client, customerId);
        await client.PostAsync($"/api/recurring-invoices/{paused.Id}/pause", null);

        await RunJobAsync();
        Assert.Empty((await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!);

        await client.PostAsync($"/api/recurring-invoices/{paused.Id}/resume", null);
        await RunJobAsync();
        Assert.Single((await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!);
    }

    [Fact]
    public async Task End_date_stops_future_generation()
    {
        var client = await OwnerAsync("rec-6@acme.se", "Rec6");
        var customerId = await CustomerAsync(client);
        await TemplateAsync(client, customerId, end: Today); // sista (och enda) perioden är idag

        await RunJobAsync();
        Assert.Single((await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!);

        Clock.Advance(TimeSpan.FromDays(40)); // nästa period ligger bortom slutdatumet
        await RunJobAsync();
        Assert.Single((await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!);
    }

    [Fact]
    public async Task Missing_customer_email_still_sends_invoice_but_logs_failed_email()
    {
        var client = await OwnerAsync("rec-5@acme.se", "Rec5");
        var noMail = await CustomerAsync(client, email: null);
        await TemplateAsync(client, noMail);

        await RunJobAsync();

        var invoices = (await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/invoices"))!;
        var inv = invoices.Single();
        Assert.Equal("Sent", inv.Status); // fakturan skickas ändå

        var emails = await client.GetFromJsonAsync<List<InvoiceEmailDto>>($"/api/invoices/{inv.Id}/emails");
        Assert.Single(emails!);
        Assert.Equal("Failed", emails![0].Status);
    }
}
