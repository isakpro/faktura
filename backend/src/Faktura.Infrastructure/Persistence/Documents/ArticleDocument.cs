using Faktura.Domain.Articles;
using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class ArticleDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    public string Name { get; set; } = "";

    [BsonIgnoreIfNull] public string? Sku { get; set; }
    [BsonIgnoreIfNull] public string? Unit { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPriceExclVat { get; set; }

    public int VatRate { get; set; }

    [BsonRepresentation(BsonType.String)]
    public ArticleStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public static ArticleDocument FromDomain(Article a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        Name = a.Name,
        Sku = a.Sku,
        Unit = a.Unit,
        UnitPriceExclVat = a.UnitPriceExclVat,
        VatRate = (int)a.VatRate,
        Status = a.Status,
        CreatedAt = a.CreatedAt
    };

    public Article ToDomain() => new(Id, TenantId, Name, Sku, Unit, UnitPriceExclVat,
        VatRateExtensions.FromPercent(VatRate), Status, CreatedAt);
}
