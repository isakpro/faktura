using Faktura.Domain.Common;
using Faktura.Domain.Invoicing;

namespace Faktura.Domain.Articles;

public enum ArticleStatus { Active = 0, Archived = 1 }

/// <summary>
/// En sparad artikel i organisationens register. Används för att förifylla fakturarader —
/// radens värden är alltid en kopia, så ändringar här rör aldrig befintliga fakturor.
/// </summary>
public sealed class Article
{
    public string Id { get; private set; }
    public string TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Sku { get; private set; }
    public string? Unit { get; private set; }
    public decimal UnitPriceExclVat { get; private set; }
    public VatRate VatRate { get; private set; }
    public ArticleStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Article(string id, string tenantId, string name, string? sku, string? unit,
        decimal unitPriceExclVat, VatRate vatRate, ArticleStatus status, DateTime createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Sku = sku;
        Unit = unit;
        UnitPriceExclVat = unitPriceExclVat;
        VatRate = vatRate;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Result<Article> CreateNew(string id, string tenantId, string? name, string? sku,
        string? unit, decimal unitPriceExclVat, int vatRatePercent, DateTime now)
    {
        var validated = Validate(name, unitPriceExclVat, vatRatePercent);
        if (validated.IsFailure) return Result.Failure<Article>(validated.Error);

        return Result.Success(new Article(id, tenantId, name!.Trim(), Normalize(sku), Normalize(unit),
            unitPriceExclVat, VatRateExtensions.FromPercent(vatRatePercent), ArticleStatus.Active, now));
    }

    public Result Update(string? name, string? sku, string? unit, decimal unitPriceExclVat, int vatRatePercent)
    {
        var validated = Validate(name, unitPriceExclVat, vatRatePercent);
        if (validated.IsFailure) return validated;

        Name = name!.Trim();
        Sku = Normalize(sku);
        Unit = Normalize(unit);
        UnitPriceExclVat = unitPriceExclVat;
        VatRate = VatRateExtensions.FromPercent(vatRatePercent);
        return Result.Success();
    }

    public void Archive() => Status = ArticleStatus.Archived;

    private static Result Validate(string? name, decimal price, int vatRatePercent)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Artikelnamn krävs."));
        if (price < 0)
            return Result.Failure(Error.Validation("Priset kan inte vara negativt."));
        if (!VatRateExtensions.IsValid(vatRatePercent))
            return Result.Failure(Error.Validation($"Ogiltig momssats: {vatRatePercent}."));
        return Result.Success();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
