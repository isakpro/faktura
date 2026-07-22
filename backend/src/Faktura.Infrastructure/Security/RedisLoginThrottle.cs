using Faktura.Domain.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Faktura.Infrastructure.Security;

/// <summary>
/// Delad inloggningsbroms mot Redis (spec 018) — ersätter <see cref="InMemoryLoginThrottle"/>
/// i produktion så bromsen gäller lika oavsett vilken instans som tar emot försöket.
/// Två nycklar per <c>key</c>: en räknare (TTL = fönstret) och en spärr (TTL = utestängningen,
/// satt när tröskeln nås) — samma semantik som in-memory-varianten.
/// </summary>
public sealed class RedisLoginThrottle : ILoginThrottle
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ThrottleOptions _options;

    public RedisLoginThrottle(IConnectionMultiplexer redis, IOptions<ThrottleOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    private static string CountKey(string key) => $"throttle:{key}:count";
    private static string BlockedKey(string key) => $"throttle:{key}:blocked";

    public bool IsBlocked(string key, out int retryAfterSeconds)
    {
        var ttl = _redis.GetDatabase().KeyTimeToLive(BlockedKey(key));
        if (ttl is { } remaining)
        {
            retryAfterSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return true;
        }
        retryAfterSeconds = 0;
        return false;
    }

    public void RecordFailure(string key)
    {
        var db = _redis.GetDatabase();
        var countKey = CountKey(key);
        var count = db.StringIncrement(countKey);
        if (count == 1) db.KeyExpire(countKey, TimeSpan.FromSeconds(_options.WindowSeconds));

        if (count >= _options.MaxAttempts)
            db.StringSet(BlockedKey(key), "1", TimeSpan.FromSeconds(_options.LockoutSeconds));
    }

    public void Reset(string key)
    {
        var db = _redis.GetDatabase();
        db.KeyDelete([CountKey(key), BlockedKey(key)]);
    }
}
