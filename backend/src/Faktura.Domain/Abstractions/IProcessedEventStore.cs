namespace Faktura.Domain.Abstractions;

/// <summary>Records processed webhook event ids to guarantee idempotent handling (FR-017).</summary>
public interface IProcessedEventStore
{
    /// <summary>
    /// Atomically marks an event id as processed. Returns <c>true</c> if it was newly recorded
    /// (proceed), or <c>false</c> if it had already been processed (skip — duplicate delivery).
    /// </summary>
    Task<bool> TryMarkProcessedAsync(string eventId, string eventType, CancellationToken ct = default);
}
