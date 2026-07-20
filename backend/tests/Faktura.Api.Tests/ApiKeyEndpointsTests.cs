using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.ApiKeys;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Members;
using Faktura.Domain.PublicApi;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 016: API-nyckelhantering (Owner/Admin).</summary>
public class ApiKeyEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ApiKeyEndpointsTests(FakturaApiFactory factory) => _factory = factory;

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
    public async Task Owner_can_create_list_and_revoke_a_key()
    {
        var a = await OwnerAsync("apikey-owner@acme.se", "ApiKeyOwner");

        var create = await a.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest("CI-integration", [ApiScopes.InvoicesRead]));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<CreatedApiKeyDto>())!;
        Assert.StartsWith("fkt_live_", created.Key);

        var list = await a.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys");
        Assert.Single(list!);
        Assert.Equal("CI-integration", list![0].Name);
        Assert.DoesNotContain(created.Key, System.Text.Json.JsonSerializer.Serialize(list)); // hela rå nyckeln läcker aldrig efteråt

        var revoke = await a.DeleteAsync($"/api/api-keys/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var listAfter = await a.GetFromJsonAsync<List<ApiKeyDto>>("/api/api-keys");
        Assert.Single(listAfter!); // finns kvar i listan (historik) men nyckeln fungerar inte längre
    }

    [Fact]
    public async Task Member_cannot_manage_keys()
    {
        var owner = await OwnerAsync("apikey-member-owner@acme.se", "ApiKeyMember");
        var invite = await owner.PostAsJsonAsync("/api/invitations", new InviteRequest("apikey-member@acme.se", "Member"));
        var token = (await invite.Content.ReadFromJsonAsync<InviteResponse>())!.Token;
        var accept = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest("password1"));
        var member = (await accept.Content.ReadFromJsonAsync<AuthResponse>())!;
        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var resp = await memberClient.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest("X", [ApiScopes.InvoicesRead]));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Invalid_scope_is_rejected()
    {
        var a = await OwnerAsync("apikey-badscope@acme.se", "ApiKeyBadScope");

        var resp = await a.PostAsJsonAsync("/api/api-keys", new CreateApiKeyRequest("X", ["not:a:real:scope"]));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
