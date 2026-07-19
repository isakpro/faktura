using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class InvoicePaymentDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string InvoiceId { get; set; } = "";

    [BsonRepresentation(BsonType.Decimal128)] public decimal Amount { get; set; }
    public DateTime PaidDate { get; set; }

    [BsonIgnoreIfNull] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public static InvoicePaymentDocument FromDomain(InvoicePayment p) => new()
    {
        Id = p.Id,
        TenantId = p.TenantId,
        InvoiceId = p.InvoiceId,
        Amount = p.Amount,
        PaidDate = p.PaidDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        Note = p.Note,
        CreatedAt = p.CreatedAt
    };

    public InvoicePayment ToDomain() =>
        new(Id, TenantId, InvoiceId, Amount, DateOnly.FromDateTime(PaidDate), Note, CreatedAt);
}
