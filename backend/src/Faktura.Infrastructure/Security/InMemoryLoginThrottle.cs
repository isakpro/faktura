using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace Faktura.Infrastructure.Security;

/// <summary>
/// In-memory login throttle (per-instance). Counts failures within a sliding window and
/// locks the key out for a period once the limit is exceeded (FR-023).
/// </summary>
public sealed class InMemoryLoginThrottle : ILoginThrottle
{
    private sealed class State
    {
        public int Count;
        public DateTime WindowStart;
        public DateTime? BlockedUntil;
    }

    private readonly ConcurrentDictionary<string, State> _states = new();
    private readonly ThrottleOptions _options;
    private readonly IClock _clock;

    public InMemoryLoginThrottle(IOptions<ThrottleOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public bool IsBlocked(string key, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (!_states.TryGetValue(key, out var state)) return false;

        lock (state)
        {
            if (state.BlockedUntil is { } until && until > _clock.UtcNow)
            {
                retryAfterSeconds = (int)Math.Ceiling((until - _clock.UtcNow).TotalSeconds);
                return true;
            }
            return false;
        }
    }

    public void RecordFailure(string key)
    {
        var now = _clock.UtcNow;
        var state = _states.GetOrAdd(key, _ => new State { WindowStart = now });

        lock (state)
        {
            // Reset the window if it has elapsed (and no active lockout).
            if (state.BlockedUntil is null && (now - state.WindowStart).TotalSeconds > _options.WindowSeconds)
            {
                state.Count = 0;
                state.WindowStart = now;
            }

            state.Count++;
            if (state.Count >= _options.MaxAttempts)
                state.BlockedUntil = now.AddSeconds(_options.LockoutSeconds);
        }
    }

    public void Reset(string key) => _states.TryRemove(key, out _);
}
