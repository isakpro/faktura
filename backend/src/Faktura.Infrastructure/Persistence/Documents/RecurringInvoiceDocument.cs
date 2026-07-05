using Faktura.Domain.Invoicing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Faktura.Infrastructure.Persistence.Documents;

internal sealed class RecurringInvoiceDocument : ITenantDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string TenantId { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string CustomerId { get; set; } = "";

    [BsonRepresentation(BsonType.String)]
    public RecurrenceInterval Interval { get; set; }

    [BsonRepresentation(BsonType.String)]
    public RecurringStatus Status { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime NextRunDate { get; set; }
    [BsonIgnoreIfNull] public DateTime? EndDate { get; set; }

    public List<InvoiceLineDocument> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private static DateTime ToDt(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static DateOnly ToDate(DateTime dt) => DateOnly.FromDateTime(dt);

    public static RecurringInvoiceDocument FromDomain(RecurringInvoice r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        CustomerId = r.CustomerId,
        Interval = r.Interval,
        Status = r.Status,
        StartDate = ToDt(r.StartDate),
        NextRunDate = ToDt(r.NextRunDate),
        EndDate = r.EndDate is { } e ? ToDt(e) : null,
        Lines = r.Lines.Select(InvoiceLineDocument.FromDomain).ToList(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };

    public RecurringInvoice ToDomain() => new(Id, TenantId, CustomerId, Interval, Status,
        ToDate(StartDate), ToDate(NextRunDate), EndDate is { } e ? ToDate(e) : null,
        Lines.Select(l => l.ToDomain()), CreatedAt, UpdatedAt);
}
