using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Billing;
using Faktura.Api.Features.Members;
using Xunit;

namespace Faktura.Api.Tests;

public class BillingEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public BillingEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient client, AuthResponse auth)> RegisterOwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static Task<HttpResponseMessage> SendWebhookAsync(HttpClient client, string id, string type, string customerId, string signature)
    {
        var payload = $$"""{"id":"{{id}}","type":"{{type}}","customerId":"{{customerId}}","subscriptionId":"sub_1"}""";
        var content = new StringContent(payload);
        content.Headers.Add("Stripe-Signature", signature);
        return client.PostAsync("/api/billing/webhook", content);
    }

    [Fact]
    public async Task Checkout_returns_url_and_billing_starts_on_free()
    {
        var (owner, _) = await RegisterOwnerAsync("bill-1@acme.se", "Bill1");

        var billing = await owner.GetFromJsonAsync<BillingDto>("/api/billing");
        Assert.Equal("Free", billing!.Plan);

        var checkout = await owner.PostAsJsonAsync("/api/billing/checkout", new CheckoutRequest("https://app/billing"));
        checkout.EnsureSuccessStatusCode();
        var body = await checkout.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.CheckoutUrl));
    }

    [Fact]
    public async Task Verified_webhook_activates_pro()
    {
        var (owner, auth) = await RegisterOwnerAsync("bill-pro@acme.se", "BillPro");
        await owner.PostAsJsonAsync("/api/billing/checkout", new CheckoutRequest("https://app")); // sets customer

        var customerId = "cus_" + auth.Organization.Id;
        var resp = await SendWebhookAsync(owner, "evt_activate", "SubscriptionActivated", customerId, "valid");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var billing = await owner.GetFromJsonAsync<BillingDto>("/api/billing");
        Assert.Equal("Pro", billing!.Plan);
        Assert.Equal("Active", billing.SubscriptionStatus);
        Assert.Equal(25, billing.SeatLimit);
    }

    [Fact]
    public async Task Webhook_with_invalid_signature_returns_400()
    {
        var (owner, auth) = await RegisterOwnerAsync("bill-badsig@acme.se", "BadSig");
        await owner.PostAsJsonAsync("/api/billing/checkout", new CheckoutRequest("https://app"));

        var resp = await SendWebhookAsync(owner, "evt_x", "SubscriptionActivated", "cus_" + auth.Organization.Id, "bad");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Duplicate_activate_event_is_idempotent()
    {
        var (owner, auth) = await RegisterOwnerAsync("bill-idem@acme.se", "Idem");
        await owner.PostAsJsonAsync("/api/billing/checkout", new CheckoutRequest("https://app"));
        var customerId = "cus_" + auth.Organization.Id;

        await SendWebhookAsync(owner, "evt_1", "SubscriptionActivated", customerId, "valid");   // -> Pro
        await SendWebhookAsync(owner, "evt_2", "SubscriptionCanceled", customerId, "valid");     // -> Free
        await SendWebhookAsync(owner, "evt_1", "SubscriptionActivated", customerId, "valid");     // duplicate id -> skipped

        var billing = await owner.GetFromJsonAsync<BillingDto>("/api/billing");
        Assert.Equal("Free", billing!.Plan); // re-delivery of evt_1 did NOT re-activate
    }

    [Fact]
    public async Task Non_owner_cannot_view_billing_403()
    {
        var (owner, _) = await RegisterOwnerAsync("bill-owner@acme.se", "OwnerOrg");

        // Create a member via invite/accept.
        var invite = await owner.PostAsJsonAsync("/api/invitations", new InviteRequest("bill-member@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<InviteResponse>())!.Token;
        var accept = await _factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;

        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var resp = await memberClient.GetAsync("/api/billing");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
