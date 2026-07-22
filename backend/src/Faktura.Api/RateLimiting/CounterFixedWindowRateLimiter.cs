using System.Threading.RateLimiting;
using Faktura.Domain.Abstractions;

namespace Faktura.Api.RateLimiting;

/// <summary>
/// Fixed-window-begränsare byggd på <see cref="IRateLimitCounter"/> i stället för den inbyggda,
/// process-lokala <c>FixedWindowRateLimiter</c> (spec 018) — så kvoten delas mellan instanser
/// när räknaren är Redis-baserad. <see cref="AttemptAcquireCore"/> är synkron; ASP.NET Core:s
/// rate limiting-middleware anropar <c>AcquireAsync</c>, vars standardimplementation delegerar
/// hit — ingen egen async-variant behövs.
/// </summary>
public sealed class CounterFixedWindowRateLimiter : RateLimiter
{
    private readonly IRateLimitCounter _counter;
    private readonly string _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public CounterFixedWindowRateLimiter(IRateLimitCounter counter, string key, int permitLimit, TimeSpan window)
    {
        _counter = counter;
        _key = key;
        _permitLimit = permitLimit;
        _window = window;
    }

    public override TimeSpan? IdleDuration => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var count = _counter.Increment(_key, _window);
        return count <= _permitLimit ? Lease.Success : Lease.Rejected(_window);
    }

    // Egen async-väg (inte bara en delegering till AttemptAcquireCore) — annars skulle varje
    // autentiserat anrop blockera en trådpool-tråd på en synkron Redis-roundtrip.
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var count = await _counter.IncrementAsync(_key, _window, cancellationToken);
        return count <= _permitLimit ? Lease.Success : Lease.Rejected(_window);
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    private sealed class Lease : RateLimitLease
    {
        public static readonly Lease Success = new(true, null);
        public static Lease Rejected(TimeSpan retryAfter) => new(false, retryAfter);

        private readonly bool _isAcquired;
        private readonly TimeSpan? _retryAfter;

        private Lease(bool isAcquired, TimeSpan? retryAfter)
        {
            _isAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired => _isAcquired;

        public override IEnumerable<string> MetadataNames =>
            _retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name && _retryAfter is { } ra)
            {
                metadata = ra;
                return true;
            }
            metadata = null;
            return false;
        }
    }
}
