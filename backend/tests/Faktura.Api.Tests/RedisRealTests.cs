using Faktura.Domain.Abstractions;
using Faktura.Infrastructure.Security;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>
/// Integrationstester mot en RIKTIG Redis (Testcontainers, spec 018). Verifierar det som
/// in-memory-fejkerna inte kan bevisa: INCR/EXPIRE-semantiken (TTL sätts bara vid första
/// ökningen) och att broms-nycklarnas TTL styr blockering/frisläppning. Skippas snyggt när
/// Docker inte är tillgängligt (körs alltid i CI).
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    public IConnectionMultiplexer? Connection { get; private set; }
    public bool Available => Connection is not null;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            Connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        }
        catch (Exception)
        {
            Connection = null; // Docker saknas — [SkippableFact] hoppar över testerna.
        }
    }

    public async Task DisposeAsync()
    {
        Connection?.Dispose();
        if (_container is not null) await _container.DisposeAsync();
    }
}

public class RedisRealTests : IClassFixture<RedisContainerFixture>
{
    private readonly RedisContainerFixture _redis;

    public RedisRealTests(RedisContainerFixture redis) => _redis = redis;

    private IConnectionMultiplexer Connection
    {
        get
        {
            Skip.IfNot(_redis.Available, "Docker/Testcontainers är inte tillgängligt på den här maskinen.");
            return _redis.Connection!;
        }
    }

    private static readonly IOptions<ThrottleOptions> Throttle = Options.Create(new ThrottleOptions
    {
        MaxAttempts = 3,
        WindowSeconds = 60,
        LockoutSeconds = 2 // kort så testet kan vänta ut frisläppningen
    });

    [SkippableFact]
    public async Task Counter_increments_and_expires_with_the_window()
    {
        var counter = new RedisRateLimitCounter(Connection);
        var key = $"test:counter:{Guid.NewGuid():N}";

        Assert.Equal(1, await counter.IncrementAsync(key, TimeSpan.FromSeconds(1)));
        Assert.Equal(2, await counter.IncrementAsync(key, TimeSpan.FromSeconds(1)));
        Assert.Equal(3, counter.Increment(key, TimeSpan.FromSeconds(1)));

        // Efter fönstret har nyckeln gått ut — räkningen börjar om, inte fortsätter.
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        Assert.Equal(1, await counter.IncrementAsync(key, TimeSpan.FromSeconds(1)));
    }

    [SkippableFact]
    public async Task Subsequent_increments_do_not_extend_the_window()
    {
        var counter = new RedisRateLimitCounter(Connection);
        var key = $"test:noextend:{Guid.NewGuid():N}";

        await counter.IncrementAsync(key, TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        await counter.IncrementAsync(key, TimeSpan.FromSeconds(2)); // får INTE förnya TTL:n

        await Task.Delay(TimeSpan.FromSeconds(1.2)); // ursprungsfönstret (2s) har nu passerat
        Assert.Equal(1, await counter.IncrementAsync(key, TimeSpan.FromSeconds(2)));
    }

    [SkippableFact]
    public async Task Throttle_blocks_after_threshold_and_releases_after_lockout()
    {
        var throttle = new RedisLoginThrottle(Connection, Throttle);
        var key = $"test:throttle:{Guid.NewGuid():N}";

        Assert.False(throttle.IsBlocked(key, out _));
        throttle.RecordFailure(key);
        throttle.RecordFailure(key);
        Assert.False(throttle.IsBlocked(key, out _)); // under tröskeln (3)

        throttle.RecordFailure(key);
        Assert.True(throttle.IsBlocked(key, out var retryAfter));
        Assert.InRange(retryAfter, 1, 2);

        await Task.Delay(TimeSpan.FromSeconds(2.5)); // vänta ut utestängningen (2s)
        Assert.False(throttle.IsBlocked(key, out _));
    }

    [SkippableFact]
    public void Reset_clears_both_counter_and_block()
    {
        var throttle = new RedisLoginThrottle(Connection, Throttle);
        var key = $"test:reset:{Guid.NewGuid():N}";

        throttle.RecordFailure(key);
        throttle.RecordFailure(key);
        throttle.RecordFailure(key);
        Assert.True(throttle.IsBlocked(key, out _));

        throttle.Reset(key);

        Assert.False(throttle.IsBlocked(key, out _));
        throttle.RecordFailure(key); // räknaren börjar om från noll efter reset
        Assert.False(throttle.IsBlocked(key, out _));
    }
}
