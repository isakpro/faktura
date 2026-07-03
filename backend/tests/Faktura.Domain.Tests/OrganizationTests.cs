using Faktura.Domain.Organizations;
using Xunit;

namespace Faktura.Domain.Tests;

public class OrganizationTests
{
    [Fact]
    public void CreateNew_starts_on_free_with_seat_limit_and_no_subscription()
    {
        var now = new DateTime(2026, 6, 28, 10, 0, 0, DateTimeKind.Utc);

        var org = Organization.CreateNew("t-1", "  Acme AB  ", freeSeatLimit: 2, now);

        Assert.Equal("t-1", org.Id);
        Assert.Equal("Acme AB", org.Name); // trimmed
        Assert.Equal(PlanTier.Free, org.Plan);
        Assert.Equal(SubscriptionStatus.None, org.SubscriptionStatus);
        Assert.Equal(2, org.SeatLimit);
        Assert.Null(org.StripeCustomerId);
        Assert.Equal(now, org.CreatedAt);
    }
}
