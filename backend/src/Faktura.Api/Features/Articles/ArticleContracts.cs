namespace Faktura.Api.Features.Articles;

public sealed record ArticleRequest(string Name, string? Sku, string? Unit, decimal UnitPriceExclVat, int VatRate);

public sealed record ArticleDto(
    string Id,
    string Name,
    string? Sku,
    string? Unit,
    decimal UnitPriceExclVat,
    int VatRate,
    string Status);
