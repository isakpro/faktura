using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Faktura.Api.Features.Members;
using Xunit;

namespace Faktura.Api.Tests;

public class AuditEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public AuditEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient client, AuthResponse auth)> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    [Fact]
    public async Task Mutations_are_logged_with_actor_and_anonymous_calls_are_not()
    {
        var (client, _) = await OwnerAsync("aud-1@acme.se", "Aud1");

        // En muterande åtgärd — skapa kund.
        await client.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));

        var entries = (await client.GetFromJsonAsync<List<AuditEntryDto>>("/api/audit"))!;
        var entry = entries.Single(e => e.Path == "/api/customers");
        Assert.Equal("POST", entry.Method);
        Assert.Equal("aud-1@acme.se", entry.ActorEmail);
        Assert.Equal(201, entry.StatusCode);

        // Anonyma anrop (registreringen) loggades inte.
        Assert.DoesNotContain(entries, e => e.Path.StartsWith("/api/auth"));
    }

    [Fact]
    public async Task Audit_log_is_tenant_isolated_and_member_gets_403()
    {
        var (a, _) = await OwnerAsync("aud-2a@acme.se", "Aud2A");
        var (b, _) = await OwnerAsync("aud-2b@acme.se", "Aud2B");
        await a.PostAsJsonAsync("/api/customers", new CustomerRequest("A-kund", null, null, null, null, 30));

        // B ser inte A:s aktivitet.
        var bEntries = (await b.GetFromJsonAsync<List<AuditEntryDto>>("/api/audit"))!;
        Assert.DoesNotContain(bEntries, e => e.Path == "/api/customers");

        // Member nekas.
        var invite = await a.PostAsJsonAsync("/api/invitations", new InviteRequest("aud-2m@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<InviteResponse>())!.Token;
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/audit")).StatusCode);
    }
}
