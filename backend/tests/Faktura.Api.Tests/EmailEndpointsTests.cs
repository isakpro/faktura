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

public class EmailEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public EmailEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private FakeEmailSender Sender => (FakeEmailSender)_factory.Services.GetRequiredService<IEmailSender>();

    private async Task<(HttpClient client, AuthResponse auth)> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<string> CustomerAsync(HttpClient client, string? email)
    {
        var resp = await client.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", email, null, null, null, 30));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
    }

    private static async Task<string> SentInvoiceAsync(HttpClient client, string customerId)
    {
        var draft = (await (await client.PostAsJsonAsync("/api/invoices",
            new CreateInvoiceRequest(customerId, [new InvoiceLineInput("Konsult", 1, 1000m, 25)]))).Content
            .ReadFromJsonAsync<InvoiceDto>())!;
        await client.PostAsync($"/api/invoices/{draft.Id}/send", null);
        return draft.Id;
    }

    private static async Task<string> DraftInvoiceAsync(HttpClient client, string customerId)
    {
        var draft = (await (await client.PostAsJsonAsync("/api/invoices",
            new CreateInvoiceRequest(customerId, [new InvoiceLineInput("Konsult", 1, 1000m, 25)]))).Content
            .ReadFromJsonAsync<InvoiceDto>())!;
        return draft.Id;
    }

    [Fact]
    public async Task Emailing_sent_invoice_delivers_pdf_and_logs()
    {
        var (client, auth) = await OwnerAsync("mail-owner@acme.se", "MailCo");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);

        var resp = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest(null));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = (await resp.Content.ReadFromJsonAsync<InvoiceEmailDto>())!;
        Assert.Equal("Sent", dto.Status);
        Assert.Equal("kund@x.se", dto.Recipient);

        var msg = Sender.LastMessage!;
        Assert.Equal("kund@x.se", msg.To);
        Assert.Equal("MailCo", msg.FromDisplayName);
        Assert.Equal(auth.User.Email, msg.ReplyTo);          // Reply-To = avsändaren
        Assert.NotNull(msg.Attachment);
        Assert.Equal("application/pdf", msg.Attachment!.ContentType);
        Assert.Contains("1", msg.Subject);                    // fakturanummer i ämnet

        var history = await client.GetFromJsonAsync<List<InvoiceEmailDto>>($"/api/invoices/{invoiceId}/emails");
        Assert.Single(history!);
        Assert.Equal("Sent", history![0].Status);
    }

    [Fact]
    public async Task Emailing_draft_is_rejected_409()
    {
        var (client, _) = await OwnerAsync("mail-draft@acme.se", "D");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var draftId = await DraftInvoiceAsync(client, customerId);

        var resp = await client.PostAsJsonAsync($"/api/invoices/{draftId}/email", new SendEmailRequest(null));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Emailing_without_recipient_returns_422()
    {
        var (client, _) = await OwnerAsync("mail-norcpt@acme.se", "N");
        var customerId = await CustomerAsync(client, email: null); // kund saknar e-post
        var invoiceId = await SentInvoiceAsync(client, customerId);

        var resp = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest(null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Override_recipient_is_used()
    {
        var (client, _) = await OwnerAsync("mail-ovr@acme.se", "O");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);

        await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest("ekonomi@kund.se"));
        Assert.Equal("ekonomi@kund.se", Sender.LastMessage!.To);
    }

    [Fact]
    public async Task Delivery_failure_returns_502_and_logs_failed_without_touching_invoice()
    {
        var (client, _) = await OwnerAsync("mail-fail@acme.se", "F");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);

        var resp = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest("fail@kund.se"));
        Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode);

        var history = await client.GetFromJsonAsync<List<InvoiceEmailDto>>($"/api/invoices/{invoiceId}/emails");
        Assert.Contains(history!, e => e.Status == "Failed");

        // Fakturan är opåverkad (fortfarande Sent).
        var invoice = await client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoiceId}");
        Assert.Equal("Sent", invoice!.Status);
    }

    [Fact]
    public async Task Repeated_emails_are_logged_separately()
    {
        var (client, _) = await OwnerAsync("mail-rep@acme.se", "R");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);

        await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest(null));
        await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/email", new SendEmailRequest("annan@kund.se"));

        var history = await client.GetFromJsonAsync<List<InvoiceEmailDto>>($"/api/invoices/{invoiceId}/emails");
        Assert.Equal(2, history!.Count);
    }

    [Fact]
    public async Task Cannot_email_another_tenants_invoice_404()
    {
        var (a, _) = await OwnerAsync("mail-xa@acme.se", "XA");
        var (b, _) = await OwnerAsync("mail-xb@acme.se", "XB");
        var custA = await CustomerAsync(a, "kund@x.se");
        var invoiceA = await SentInvoiceAsync(a, custA);

        var resp = await b.PostAsJsonAsync($"/api/invoices/{invoiceA}/email", new SendEmailRequest(null));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
