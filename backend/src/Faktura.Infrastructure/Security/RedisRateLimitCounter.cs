using Faktura.Domain.Abstractions;
using StackExchange.Redis;

namespace Faktura.Infrastructure.Security;

/// <summary>
/// Distribuerad fixed-window-räknare mot Redis (spec 018): <c>INCR</c> ökar nyckeln,
/// <c>EXPIRE</c> sätts bara vid den första ökningen (count == 1) så fönstret inte förlängs
/// av efterföljande anrop.
/// </summary>
public sealed class RedisRateLimitCounter : IRateLimitCounter
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitCounter(IConnectionMultiplexer redis) => _redis = redis;

    public long Increment(string key, TimeSpan window)
    {
        var db = _redis.GetDatabase();
        var count = db.StringIncrement(key);
        if (count == 1) db.KeyExpire(key, window);
        return count;
    }

    public async Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var count = await db.StringIncrementAsync(key);
        if (count == 1) await db.KeyExpireAsync(key, window);
        return count;
    }
}
