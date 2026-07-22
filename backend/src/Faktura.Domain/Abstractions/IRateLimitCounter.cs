namespace Faktura.Domain.Abstractions;

/// <summary>
/// Distribuerad fixed-window-räknare för rate limiting (spec 018) — delad mellan instanser,
/// till skillnad från en process-lokal räknare. TTL sätts vid nyckelns första ökning.
/// </summary>
public interface IRateLimitCounter
{
    /// <summary>Ökar räknaren för <paramref name="key"/> och returnerar det nya värdet.</summary>
    long Increment(string key, TimeSpan window);

    /// <summary>Asynkron variant — används på ASP.NET Core:s async rate limiting-hetväg (per request).</summary>
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct = default);
}
