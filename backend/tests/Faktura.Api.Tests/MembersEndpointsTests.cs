using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Members;
using Xunit;

namespace Faktura.Api.Tests;

public class MembersEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public MembersEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient client, AuthResponse auth)> RegisterOwnerAsync(string email, string org = "Acme AB")
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<string> InviteAsync(HttpClient owner, string email, string role = "Member")
    {
        var resp = await owner.PostAsJsonAsync("/api/invitations", new InviteRequest(email, role));
        resp.EnsureSuccessStatusCode();
        var body = (await resp.Content.ReadFromJsonAsync<InviteResponse>())!;
        return body.Token;
    }

    private async Task<AuthResponse> AcceptAsync(string token, string password = "password1")
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync($"/api/invitations/{token}/accept", new AcceptInvitationRequest(password));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Members_list_shows_only_own_tenant()
    {
        var (a, _) = await RegisterOwnerAsync("iso-a@acme.se", "OrgA");
        await RegisterOwnerAsync("iso-b@acme.se", "OrgB"); // separate tenant with its own owner

        var members = await a.GetFromJsonAsync<List<MemberDto>>("/api/members");

        Assert.NotNull(members);
        Assert.Single(members!);
        Assert.Equal("iso-a@acme.se", members![0].Email); // never sees OrgB's owner
    }

    [Fact]
    public async Task Invite_then_accept_adds_a_member()
    {
        var (owner, _) = await RegisterOwnerAsync("team-owner@acme.se", "Team");
        var token = await InviteAsync(owner, "team-member@acme.se");

        var member = await AcceptAsync(token);
        Assert.Equal("team-member@acme.se", member.User.Email);
        Assert.Equal("Member", member.User.Role);

        var members = await owner.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Equal(2, members!.Count);
    }

    [Fact]
    public async Task Member_cannot_invite_others_403()
    {
        var (owner, _) = await RegisterOwnerAsync("rbac-owner@acme.se", "Rbac");
        var token = await InviteAsync(owner, "rbac-member@acme.se");
        var member = await AcceptAsync(token);

        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var resp = await memberClient.PostAsJsonAsync("/api/invitations", new InviteRequest("x@acme.se", "Member"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Free_seat_limit_blocks_third_member()
    {
        var (owner, _) = await RegisterOwnerAsync("seat-owner@acme.se", "Seats"); // seat 1/2
        var token = await InviteAsync(owner, "seat-2@acme.se");
        await AcceptAsync(token); // seat 2/2

        var resp = await owner.PostAsJsonAsync("/api/invitations", new InviteRequest("seat-3@acme.se", "Member"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // seat_limit
    }

    [Fact]
    public async Task Admin_cannot_grant_owner_role_403()
    {
        var (owner, _) = await RegisterOwnerAsync("adm-owner@acme.se", "AdmOrg");
        var token = await InviteAsync(owner, "adm-user@acme.se", "Admin");
        var admin = await AcceptAsync(token);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        // Admin tries to promote itself to Owner.
        var resp = await adminClient.PutAsJsonAsync($"/api/members/{admin.User.Id}/role", new ChangeRoleRequest("Owner"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Cannot_demote_the_last_owner_409()
    {
        var (owner, auth) = await RegisterOwnerAsync("last-owner@acme.se", "Solo");

        var resp = await owner.PutAsJsonAsync($"/api/members/{auth.User.Id}/role", new ChangeRoleRequest("Member"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // last_owner
    }

    [Fact]
    public async Task Cannot_change_role_of_another_tenants_user_404()
    {
        var (a, _) = await RegisterOwnerAsync("xt-a@acme.se", "XtA");
        var (_, bAuth) = await RegisterOwnerAsync("xt-b@acme.se", "XtB");

        // A attempts to change B's owner role -> not found in A's tenant.
        var resp = await a.PutAsJsonAsync($"/api/members/{bAuth.User.Id}/role", new ChangeRoleRequest("Member"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Members_endpoint_requires_auth_401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/members");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_can_remove_member_and_their_refresh_token_dies()
    {
        var (owner, _) = await RegisterOwnerAsync("rm-owner@acme.se", "RmOrg");
        var token = await InviteAsync(owner, "rm-member@acme.se");
        var member = await AcceptAsync(token);

        var resp = await owner.DeleteAsync($"/api/members/{member.User.Id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var members = await owner.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.DoesNotContain(members!, m => m.Id == member.User.Id);

        // Den borttagna användarens refresh-token är död (användaren finns inte längre).
        var refresh = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(member.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Member_cannot_remove_others_403()
    {
        var (owner, ownerAuth) = await RegisterOwnerAsync("rm2-owner@acme.se", "Rm2");
        var token = await InviteAsync(owner, "rm2-member@acme.se");
        var member = await AcceptAsync(token);

        var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var resp = await memberClient.DeleteAsync($"/api/members/{ownerAuth.User.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_remove_owner_403()
    {
        var (owner, ownerAuth) = await RegisterOwnerAsync("rm3-owner@acme.se", "Rm3");
        var token = await InviteAsync(owner, "rm3-admin@acme.se", "Admin");
        var admin = await AcceptAsync(token);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var resp = await adminClient.DeleteAsync($"/api/members/{ownerAuth.User.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Cannot_remove_last_owner_409()
    {
        var (owner, auth) = await RegisterOwnerAsync("rm4-owner@acme.se", "Rm4");

        var resp = await owner.DeleteAsync($"/api/members/{auth.User.Id}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode); // last_owner
    }

    [Fact]
    public async Task Cannot_remove_another_tenants_member_404()
    {
        var (a, _) = await RegisterOwnerAsync("rm5-a@acme.se", "Rm5A");
        var (_, bAuth) = await RegisterOwnerAsync("rm5-b@acme.se", "Rm5B");

        var resp = await a.DeleteAsync($"/api/members/{bAuth.User.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
