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

public class ReminderEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ReminderEndpointsTests(FakturaApiFactory factory) => _factory = factory;

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

    private static async Task<string> CustomerAsync(HttpClient client, string? email)
    {
        var resp = await client.PostAsJsonAsync("/api/customers",
            new CustomerRequest("Kund AB", email, null, null, null, 30));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CustomerDto>())!.Id;
    }

    /// <summary>Skapar och skickar en faktura (förfaller om 30 dagar från fejk-nu).</summary>
    private static async Task<string> SentInvoiceAsync(HttpClient client, string customerId)
    {
        var draft = (await (await client.PostAsJsonAsync("/api/invoices",
            new CreateInvoiceRequest(customerId, [new InvoiceLineInput("Konsult", 1, 1000m, 25)]))).Content
            .ReadFromJsonAsync<InvoiceDto>())!;
        var send = await client.PostAsync($"/api/invoices/{draft.Id}/send", null);
        send.EnsureSuccessStatusCode();
        return draft.Id;
    }

    private void MakeOverdue(int extraDays = 1) => Clock.Advance(TimeSpan.FromDays(30 + extraDays));

    private async Task<int> RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunOnceAsync();
    }

    [Fact]
    public async Task Manual_remind_on_overdue_sends_pdf_and_sequence_increments()
    {
        var client = await OwnerAsync("rem-1@acme.se", "Rem1");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);
        MakeOverdue();

        var first = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/remind", new RemindRequest(null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var dto = (await first.Content.ReadFromJsonAsync<InvoiceReminderDto>())!;
        Assert.Equal(1, dto.Sequence);
        Assert.Equal("Manual", dto.Type);
        Assert.Contains("Påminnelse 1", dto.Subject);

        var msg = Sender.LastMessage!;
        Assert.Equal("kund@x.se", msg.To);
        Assert.NotNull(msg.Attachment);
        Assert.Equal("application/pdf", msg.Attachment!.ContentType);
        Assert.Contains("förföll", msg.Body);

        // Upprepad påminnelse får nr 2.
        var second = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/remind", new RemindRequest(null));
        Assert.Equal(2, (await second.Content.ReadFromJsonAsync<InvoiceReminderDto>())!.Sequence);

        var history = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{invoiceId}/reminders");
        Assert.Equal(2, history!.Count);
    }

    [Fact]
    public async Task Remind_rejects_not_overdue_draft_and_paid_409()
    {
        var client = await OwnerAsync("rem-2@acme.se", "Rem2");
        var customerId = await CustomerAsync(client, "kund@x.se");

        // Nyss skickad (ej förfallen).
        var freshId = await SentInvoiceAsync(client, customerId);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/invoices/{freshId}/remind", new RemindRequest(null))).StatusCode);

        // Utkast.
        var draft = (await (await client.PostAsJsonAsync("/api/invoices",
            new CreateInvoiceRequest(customerId, [new InvoiceLineInput("A", 1, 1m, 0)]))).Content
            .ReadFromJsonAsync<InvoiceDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/invoices/{draft.Id}/remind", new RemindRequest(null))).StatusCode);

        // Betald (förfallen men betald).
        var paidId = await SentInvoiceAsync(client, customerId);
        MakeOverdue();
        await client.PostAsJsonAsync($"/api/invoices/{paidId}/mark-paid",
            new MarkPaidRequest(DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/invoices/{paidId}/remind", new RemindRequest(null))).StatusCode);
    }

    [Fact]
    public async Task Remind_missing_recipient_422_and_override_works()
    {
        var client = await OwnerAsync("rem-3@acme.se", "Rem3");
        var noMail = await CustomerAsync(client, email: null);
        var invoiceId = await SentInvoiceAsync(client, noMail);
        MakeOverdue();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/remind", new RemindRequest(null))).StatusCode);

        var overridden = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/remind",
            new RemindRequest("ekonomi@kund.se"));
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        Assert.Equal("ekonomi@kund.se", Sender.LastMessage!.To);
    }

    [Fact]
    public async Task Remind_delivery_failure_502_logs_failed_invoice_untouched()
    {
        var client = await OwnerAsync("rem-4@acme.se", "Rem4");
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);
        MakeOverdue();

        var resp = await client.PostAsJsonAsync($"/api/invoices/{invoiceId}/remind", new RemindRequest("fail@kund.se"));
        Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode);

        var history = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{invoiceId}/reminders");
        Assert.Contains(history!, r => r.Status == "Failed");

        var invoice = await client.GetFromJsonAsync<InvoiceDto>($"/api/invoices/{invoiceId}");
        Assert.Equal("Overdue", invoice!.Status); // opåverkad (härledd förfallen, ej muterad)
    }

    [Fact]
    public async Task Cross_tenant_remind_404()
    {
        var a = await OwnerAsync("rem-5a@acme.se", "Rem5A");
        var b = await OwnerAsync("rem-5b@acme.se", "Rem5B");
        var custA = await CustomerAsync(a, "kund@x.se");
        var invoiceA = await SentInvoiceAsync(a, custA);
        MakeOverdue();

        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsJsonAsync($"/api/invoices/{invoiceA}/remind", new RemindRequest(null))).StatusCode);
    }

    [Fact]
    public async Task Settings_default_off_and_only_owner_admin_can_update()
    {
        var owner = await OwnerAsync("rem-6@acme.se", "Rem6");

        var defaults = await owner.GetFromJsonAsync<ReminderSettingsDto>("/api/reminder-settings");
        Assert.False(defaults!.AutoEnabled);
        Assert.Equal(7, defaults.DaysAfterDue);

        // Member nekas.
        var invite = await owner.PostAsJsonAsync("/api/invitations",
            new Faktura.Api.Features.Members.InviteRequest("rem-6m@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<Faktura.Api.Features.Members.InviteResponse>())!.Token;
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{token}/accept", new Faktura.Api.Features.Members.AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await memberClient.PutAsJsonAsync("/api/reminder-settings", new ReminderSettingsDto(true, 5))).StatusCode);

        // Owner kan uppdatera.
        var updated = await owner.PutAsJsonAsync("/api/reminder-settings", new ReminderSettingsDto(true, 10));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var readBack = await owner.GetFromJsonAsync<ReminderSettingsDto>("/api/reminder-settings");
        Assert.True(readBack!.AutoEnabled);
        Assert.Equal(10, readBack.DaysAfterDue);
    }

    [Fact]
    public async Task Job_sends_exactly_one_automatic_reminder_without_duplicates()
    {
        var client = await OwnerAsync("rem-7@acme.se", "Rem7");
        await client.PutAsJsonAsync("/api/reminder-settings", new ReminderSettingsDto(true, 7));
        var customerId = await CustomerAsync(client, "kund@x.se");
        var invoiceId = await SentInvoiceAsync(client, customerId);
        MakeOverdue(extraDays: 8); // förfallen i 8 dagar >= 7

        await RunJobAsync();
        var history = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{invoiceId}/reminders");
        Assert.Single(history!);
        Assert.Equal("Automatic", history![0].Type);
        Assert.Null(Sender.LastMessage!.ReplyTo); // automatiska utskick saknar Reply-To

        // Omkörning ger ingen dubblett.
        await RunJobAsync();
        history = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{invoiceId}/reminders");
        Assert.Single(history!);
    }

    [Fact]
    public async Task Job_respects_disabled_setting_and_skips_paid()
    {
        var client = await OwnerAsync("rem-8@acme.se", "Rem8"); // automatik av (default)
        var customerId = await CustomerAsync(client, "kund@x.se");
        var overdueId = await SentInvoiceAsync(client, customerId);
        var paidId = await SentInvoiceAsync(client, customerId);
        MakeOverdue(extraDays: 10);
        await client.PostAsJsonAsync($"/api/invoices/{paidId}/mark-paid",
            new MarkPaidRequest(DateOnly.FromDateTime(DateTime.UtcNow)));

        await RunJobAsync();
        var h1 = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{overdueId}/reminders");
        Assert.Empty(h1!); // automatik av -> inget

        // Slå på: den förfallna påminns, den betalda hoppas över.
        await client.PutAsJsonAsync("/api/reminder-settings", new ReminderSettingsDto(true, 7));
        await RunJobAsync();
        h1 = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{overdueId}/reminders");
        var h2 = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{paidId}/reminders");
        Assert.Single(h1!);
        Assert.Empty(h2!);
    }

    [Fact]
    public async Task Job_logs_missing_email_as_failed_and_continues_with_others()
    {
        var client = await OwnerAsync("rem-9@acme.se", "Rem9");
        await client.PutAsJsonAsync("/api/reminder-settings", new ReminderSettingsDto(true, 7));
        var noMail = await CustomerAsync(client, email: null);
        var withMail = await CustomerAsync(client, "kund@x.se");
        var badId = await SentInvoiceAsync(client, noMail);
        var goodId = await SentInvoiceAsync(client, withMail);
        MakeOverdue(extraDays: 8);

        await RunJobAsync();

        var bad = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{badId}/reminders");
        var good = await client.GetFromJsonAsync<List<InvoiceReminderDto>>($"/api/invoices/{goodId}/reminders");
        Assert.Single(bad!);
        Assert.Equal("Failed", bad![0].Status);   // loggad men stoppade inte jobbet
        Assert.Single(good!);
        Assert.Equal("Sent", good![0].Status);
    }
}
