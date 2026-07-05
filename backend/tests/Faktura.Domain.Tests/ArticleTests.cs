using Faktura.Domain.Articles;
using Faktura.Domain.Invoicing;
using Xunit;

namespace Faktura.Domain.Tests;

public class ArticleTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateNew_normalizes_and_starts_active()
    {
        var result = Article.CreateNew("a-1", "t-1", "  Konsulttimme ", " K-100 ", " tim ", 1200m, 25, Now);

        Assert.True(result.IsSuccess);
        var a = result.Value;
        Assert.Equal("Konsulttimme", a.Name);
        Assert.Equal("K-100", a.Sku);
        Assert.Equal("tim", a.Unit);
        Assert.Equal(VatRate.TwentyFive, a.VatRate);
        Assert.Equal(ArticleStatus.Active, a.Status);
    }

    [Fact]
    public void CreateNew_allows_missing_sku_and_unit()
    {
        var result = Article.CreateNew("a", "t", "Bok", sku: "  ", unit: null, 100m, 6, Now);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Sku);
        Assert.Null(result.Value.Unit);
    }

    [Theory]
    [InlineData(null, 100, 25)]   // namn saknas
    [InlineData("", 100, 25)]
    [InlineData("X", -1, 25)]     // negativt pris
    [InlineData("X", 100, 13)]    // ogiltig momssats
    public void CreateNew_rejects_invalid(string? name, decimal price, int vat)
    {
        Assert.True(Article.CreateNew("a", "t", name, null, null, price, vat, Now).IsFailure);
    }

    [Fact]
    public void Update_and_archive_work()
    {
        var a = Article.CreateNew("a", "t", "Bok", null, null, 100m, 6, Now).Value;

        Assert.True(a.Update("E-bok", "B-2", "st", 80m, 25).IsSuccess);
        Assert.Equal("E-bok", a.Name);
        Assert.Equal(VatRate.TwentyFive, a.VatRate);

        a.Archive();
        Assert.Equal(ArticleStatus.Archived, a.Status);
    }

    [Fact]
    public void InvoiceLine_carries_optional_unit()
    {
        var with = new InvoiceLine("Konsult", 10, 1200m, VatRate.TwentyFive, "tim");
        var without = new InvoiceLine("Konsult", 10, 1200m, VatRate.TwentyFive);

        Assert.Equal("tim", with.Unit);
        Assert.Null(without.Unit);
        Assert.Equal(with.Net, without.Net); // enheten påverkar inte beräkningen
    }
}
