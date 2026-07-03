using Faktura.Domain.Abstractions;
using Faktura.Infrastructure.Persistence;
using Faktura.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Billing;

/// <summary>
/// Idempotency via a unique event id: inserting an already-seen id fails on the unique
/// _id, which we treat as "already processed".
/// </summary>
internal sealed class MongoProcessedEventStore : IProcessedEventStore
{
    private readonly MongoContext _context;
    private readonly IClock _clock;

    public MongoProcessedEventStore(MongoContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<bool> TryMarkProcessedAsync(string eventId, string eventType, CancellationToken ct = default)
    {
        try
        {
            await _context.ProcessedEvents.InsertOneAsync(
                new ProcessedEventDocument { Id = eventId, Type = eventType, ProcessedAt = _clock.UtcNow },
                cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false; // already processed
        }
    }
}
