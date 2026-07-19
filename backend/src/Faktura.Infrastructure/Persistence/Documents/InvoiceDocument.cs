using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class InvoiceLineDocument
{
    public string Description { get; set; } = "";

    [BsonRepresentation(BsonType.Decimal128)] public decimal Quantity { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal UnitPriceExclVat { get; set; }
    public int VatRate { get; set; }

    [BsonIgnoreIfNull] public string? Unit { get; set; }

    public static InvoiceLineDocument FromDomain(InvoiceLine l) => new()
    {
        Description = l.Description,
        Quantity = l.Quantity,
        UnitPriceExclVat = l.UnitPriceExclVat,
        VatRate = (int)l.VatRate,
        Unit = l.Unit
    };

    public InvoiceLine ToDomain() =>
        new(Description, Quantity, UnitPriceExclVat, VatRateExtensions.FromPercent(VatRate), Unit);
}

internal sealed class CustomerSnapshotDocument
{
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? OrgNumber { get; set; }
    public string? VatNumber { get; set; }
    public AddressDocument? Address { get; set; }

    public static CustomerSnapshotDocument? FromDomain(CustomerSnapshot? s) => s is null ? null : new()
    {
        Name = s.Name,
        Email = s.Email,
        OrgNumber = s.OrgNumber,
        VatNumber = s.VatNumber,
        Address = AddressDocument.FromDomain(s.Address)
    };

    public CustomerSnapshot ToDomain() => new(Name, Email, OrgNumber, VatNumber, Address?.ToDomain());
}

internal sealed class InvoiceDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string CustomerId { get; set; } = "";

    public CustomerSnapshotDocument? CustomerSnapshot { get; set; }

    [BsonRepresentation(BsonType.String)] public InvoiceType Type { get; set; }
    [BsonRepresentation(BsonType.String)] public InvoiceStatus Status { get; set; }

    [BsonIgnoreIfNull] public long? Number { get; set; }
    [BsonIgnoreIfNull] public DateTime? InvoiceDate { get; set; }
    [BsonIgnoreIfNull] public DateTime? DueDate { get; set; }
    [BsonIgnoreIfNull] public DateTime? PaidDate { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? OriginalInvoiceId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? RecurringSourceId { get; set; }

    [BsonRepresentation(BsonType.Decimal128)] public decimal CreditedAmount { get; set; }

    // Spec 012 — saknas på äldre dokument: OCR blir null och betalt belopp 0 (bakåtkompatibelt).
    [BsonIgnoreIfNull] public string? OcrNumber { get; set; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal PaidAmount { get; set; }

    public List<InvoiceLineDocument> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private static DateTime? ToDt(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static DateOnly? ToDate(DateTime? dt) => dt is null ? null : DateOnly.FromDateTime(dt.Value);

    public static InvoiceDocument FromDomain(Invoice i) => new()
    {
        Id = i.Id,
        TenantId = i.TenantId,
        CustomerId = i.CustomerId,
        CustomerSnapshot = CustomerSnapshotDocument.FromDomain(i.CustomerSnapshot),
        Type = i.Type,
        Status = i.Status,
        Number = i.Number,
        InvoiceDate = ToDt(i.InvoiceDate),
        DueDate = ToDt(i.DueDate),
        PaidDate = ToDt(i.PaidDate),
        OriginalInvoiceId = i.OriginalInvoiceId,
        RecurringSourceId = i.RecurringSourceId,
        CreditedAmount = i.CreditedAmount,
        OcrNumber = i.OcrNumber,
        PaidAmount = i.PaidAmount,
        Lines = i.Lines.Select(InvoiceLineDocument.FromDomain).ToList(),
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };

    public Invoice ToDomain() => new(
        Id, TenantId, CustomerId, CustomerSnapshot?.ToDomain(), Type, Status, Number,
        ToDate(InvoiceDate), ToDate(DueDate), ToDate(PaidDate), OriginalInvoiceId, CreditedAmount,
        Lines.Select(l => l.ToDomain()), CreatedAt, UpdatedAt, RecurringSourceId, OcrNumber, PaidAmount);
}
