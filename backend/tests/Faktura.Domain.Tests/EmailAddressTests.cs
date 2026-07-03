using Faktura.Domain.Authentication;
using Xunit;

namespace Faktura.Domain.Tests;

public class EmailAddressTests
{
    [Fact]
    public void Normalizes_trim_and_lowercase()
    {
        var result = EmailAddress.Create("  Owner@Acme.SE ");

        Assert.True(result.IsSuccess);
        Assert.Equal("owner@acme.se", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public void Rejects_invalid(string? raw)
    {
        Assert.True(EmailAddress.Create(raw).IsFailure);
    }
}
