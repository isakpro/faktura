using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class InvoiceEmailDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string InvoiceId { get; set; } = "";

    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public InvoiceEmailStatus Status { get; set; }

    public string? Error { get; set; }
    public DateTime SentAt { get; set; }

    public static InvoiceEmailDocument FromDomain(InvoiceEmail e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        InvoiceId = e.InvoiceId,
        Recipient = e.Recipient,
        Subject = e.Subject,
        Status = e.Status,
        Error = e.Error,
        SentAt = e.SentAt
    };

    public InvoiceEmail ToDomain() => new(Id, TenantId, InvoiceId, Recipient, Subject, Status, Error, SentAt);
}
