using Faktura.Domain.Abstractions;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Faktura.Infrastructure.Persistence;

internal sealed class InvoiceCounterDocument
{
    [BsonId] public string Id { get; set; } = ""; // = tenantId
    public long Seq { get; set; }
}

/// <summary>
/// Atomisk nummerserie per tenant via <c>FindOneAndUpdate</c> med <c>$inc</c> (upsert). Två
/// samtidiga skick kan aldrig få samma nummer eller skapa hopp.
/// </summary>
internal sealed class MongoInvoiceNumberSequence : IInvoiceNumberSequence
{
    private readonly MongoContext _context;

    public MongoInvoiceNumberSequence(MongoContext context) => _context = context;

    public async Task<long> NextAsync(string tenantId, CancellationToken ct = default)
    {
        var update = Builders<InvoiceCounterDocument>.Update.Inc(c => c.Seq, 1);
        var options = new FindOneAndUpdateOptions<InvoiceCounterDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var counter = await _context.InvoiceCounters.FindOneAndUpdateAsync<InvoiceCounterDocument>(
            c => c.Id == tenantId, update, options, ct);

        return counter.Seq;
    }
}
