using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;

namespace Faktura.Api.Tests.Fakes;

/// <summary>
/// Process-lokal motsvarighet till RedisRateLimitCounter (spec 018) för testsviten —
/// samma fixed-window-semantik (räknare + fönster-TTL), utan extern Redis.
/// </summary>
public sealed class InMemoryRateLimitCounter : IRateLimitCounter
{
    private sealed class Entry
    {
        public long Count;
        public DateTime ExpiresAt;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public long Increment(string key, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var entry = _entries.GetOrAdd(key, _ => new Entry { ExpiresAt = now + window });
        lock (entry)
        {
            if (entry.ExpiresAt <= now)
            {
                entry.Count = 0;
                entry.ExpiresAt = now + window;
            }
            return ++entry.Count;
        }
    }

    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct = default)
        => Task.FromResult(Increment(key, window));
}
