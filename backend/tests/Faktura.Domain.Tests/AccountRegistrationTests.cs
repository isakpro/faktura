using Faktura.Domain.Authentication;
using Faktura.Domain.Organizations;
using Faktura.Domain.Tests.Fakes;
using Faktura.Domain.Users;
using Xunit;

namespace Faktura.Domain.Tests;

public class AccountRegistrationTests
{
    private static AccountRegistration CreateSut() => new(
        new FakePasswordHasher(),
        new SequentialIdGenerator(),
        new FixedClock(new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc)),
        new TestPlanCatalog());

    [Fact]
    public void Register_creates_free_org_and_owner_with_hashed_normalized_credentials()
    {
        var result = CreateSut().Register("Acme AB", "  Owner@Acme.SE ", "password1");

        Assert.True(result.IsSuccess);
        var (org, owner) = (result.Value.Organization, result.Value.Owner);

        Assert.Equal(PlanTier.Free, org.Plan);
        Assert.Equal(2, org.SeatLimit);

        Assert.Equal(org.Id, owner.TenantId);              // owner belongs to the new tenant
        Assert.Equal(UserRole.Owner, owner.Role);
        Assert.Equal(UserStatus.Active, owner.Status);     // active immediately (no email verification)
        Assert.Equal("owner@acme.se", owner.Email);        // normalized
        Assert.Equal("hashed:password1", owner.PasswordHash); // hashed, never plaintext
    }

    [Theory]
    [InlineData("", "owner@acme.se", "password1", "validation")]      // missing org name
    [InlineData("Acme", "bad-email", "password1", "validation")]      // invalid email
    [InlineData("Acme", "owner@acme.se", "weak", "weak_password")]    // weak password
    public void Register_rejects_invalid_input(string org, string email, string pwd, string expectedCode)
    {
        var result = CreateSut().Register(org, email, pwd);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }
}
