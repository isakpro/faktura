using Faktura.Domain.Authentication;
using Xunit;

namespace Faktura.Domain.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short1")]      // < 8
    [InlineData("password")]    // no digit
    [InlineData("12345678")]    // no letter
    public void Rejects_weak_passwords(string password)
    {
        var result = PasswordPolicy.Validate(password);

        Assert.True(result.IsFailure);
        Assert.Equal("weak_password", result.Error.Code);
    }

    [Theory]
    [InlineData("password1")]
    [InlineData("Sommar2026")]
    public void Accepts_valid_passwords(string password)
    {
        Assert.True(PasswordPolicy.Validate(password).IsSuccess);
    }
}
