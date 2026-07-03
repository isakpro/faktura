using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Xunit;

namespace Faktura.Api.Tests;

public class AuthEndpointsTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public AuthEndpointsTests(FakturaApiFactory factory) => _factory = factory;

    private static RegisterRequest NewOrg(string email) => new("Acme AB", email, "password1");

    [Fact]
    public async Task Register_creates_org_and_owner_and_returns_tokens()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewOrg("owner@acme-1.se"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("owner@acme-1.se", body.User.Email);
        Assert.Equal("Owner", body.User.Role);
        Assert.Equal("Free", body.Organization.Plan);
        Assert.Equal(2, body.Organization.SeatLimit);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewOrg("dup@acme.se"));

        var second = await client.PostAsJsonAsync("/api/auth/register", NewOrg("dup@acme.se"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_422()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Acme", "weakpwd@acme.se", "weak"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewOrg("login-ok@acme.se"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("login-ok@acme.se", "password1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewOrg("login-bad@acme.se"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("login-bad@acme.se", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_are_throttled_with_429_and_retry_after()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewOrg("throttle@acme.se"));

        // MaxAttempts = 3 (test config): three wrong passwords, then locked out.
        for (var i = 0; i < 3; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("throttle@acme.se", "wrong-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("throttle@acme.se", "password1")); // even correct pw is blocked now
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_token_returns_current_user_and_org()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register", NewOrg("me@acme.se"));
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");

        Assert.NotNull(me);
        Assert.Equal("me@acme.se", me!.User.Email);
        Assert.Equal(auth.Organization.Id, me.Organization.Id);
        Assert.Equal("Owner", me.User.Role);
    }

    [Fact]
    public async Task Refresh_rotates_and_returns_new_tokens()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register", NewOrg("refresh@acme.se"));
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.RefreshToken));
        Assert.NotEqual(auth.RefreshToken, tokens.RefreshToken); // rotated

        // The old refresh token is now revoked.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
