using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Faktura.Api.Features.Auth;
using Faktura.Api.Features.Customers;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>Spec 017: realtidskanalen — mottagning, feltolerans-fri auth, tenant-isolering.</summary>
public class ActivityHubTests : IClassFixture<FakturaApiFactory>
{
    private readonly FakturaApiFactory _factory;

    public ActivityHubTests(FakturaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string AccessToken)> OwnerAsync(string email, string org)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(org, email, "password1"));
        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.AccessToken);
    }

    private HubConnection Connect(string? accessToken)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/activity"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling; // TestServer stödjer inte riktiga WebSockets
                if (accessToken is not null) options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            });
        return builder.Build();
    }

    [Fact]
    public async Task Mutating_request_is_broadcast_to_the_connected_client()
    {
        var (owner, token) = await OwnerAsync("hub-basic@acme.se", "HubBasic");
        await using var connection = Connect(token);

        var received = new TaskCompletionSource<JsonElement>();
        connection.On<JsonElement>("activity", msg => received.TrySetResult(msg));
        await connection.StartAsync();

        await owner.PostAsJsonAsync("/api/customers", new CustomerRequest("Kund AB", null, null, null, null, 30));

        var msg = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("POST", msg.GetProperty("method").GetString());
        Assert.Equal("/api/customers", msg.GetProperty("path").GetString());
        Assert.Equal("hub-basic@acme.se", msg.GetProperty("actorEmail").GetString());
    }

    [Fact]
    public async Task Events_never_cross_tenants()
    {
        var (ownerA, tokenA) = await OwnerAsync("hub-iso-a@acme.se", "HubIsoA");
        var (ownerB, tokenB) = await OwnerAsync("hub-iso-b@acme.se", "HubIsoB");

        await using var connA = Connect(tokenA);
        await using var connB = Connect(tokenB);

        var receivedA = new TaskCompletionSource<JsonElement>();
        var receivedB = new TaskCompletionSource<JsonElement>();
        connA.On<JsonElement>("activity", msg => receivedA.TrySetResult(msg));
        connB.On<JsonElement>("activity", msg => receivedB.TrySetResult(msg));
        await connA.StartAsync();
        await connB.StartAsync();

        await ownerA.PostAsJsonAsync("/api/customers", new CustomerRequest("A:s kund", null, null, null, null, 30));

        var msg = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("hub-iso-a@acme.se", msg.GetProperty("actorEmail").GetString());

        // B:s uppkoppling ska inte ha fått något inom rimlig tid.
        var bGotSomething = await Task.WhenAny(receivedB.Task, Task.Delay(500)) == receivedB.Task;
        Assert.False(bGotSomething);
        _ = ownerB; // används bara för att registrera tenant B
    }

    [Fact]
    public async Task Connection_without_a_valid_token_is_rejected()
    {
        await using var connection = Connect(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }
}
