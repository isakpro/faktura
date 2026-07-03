using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faktura.Api.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Faktura.Api.Tests;

public class RateLimitingTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public RateLimitingTests(FakturaApiFactory factory) => _factory = factory;

    // Dedicated host with a tiny Free quota so throttling is deterministic and isolated
    // from the other test classes (which use the default, higher quota).
    private WebApplicationFactory<Program> LowQuotaFactory() => _factory.WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Plans:Free:RateLimitPermitLimit"] = "4",
            ["Plans:Free:RateLimitWindowSeconds"] = "60"
        })));

    private static async Task<HttpClient> AuthedClientAsync(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Q", email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    [Fact]
    public async Task Tenant_over_quota_gets_429_with_retry_after_and_other_tenant_unaffected()
    {
        var factory = LowQuotaFactory();

        var a = await AuthedClientAsync(factory.CreateClient(), "rl-a@acme.se");
        var b = await AuthedClientAsync(factory.CreateClient(), "rl-b@acme.se");

        // Free quota = 4 requests/window. First 4 authenticated calls for A succeed.
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/me")).StatusCode);

        // The 5th is rejected with 429 + Retry-After.
        var throttled = await a.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.NotNull(throttled.Headers.RetryAfter);

        // Tenant B is in its own partition and is unaffected.
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/me")).StatusCode);
    }
}
