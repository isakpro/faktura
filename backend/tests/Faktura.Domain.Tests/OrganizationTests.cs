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

    [Fact]
    public void ActivatePro_sets_pro_active_and_seat_limit()
    {
        var org = Organization.CreateNew("t-1", "Acme", freeSeatLimit: 2, DateTime.UtcNow);

        org.ActivatePro("sub_123", proSeatLimit: 25);

        Assert.Equal(PlanTier.Pro, org.Plan);
        Assert.Equal(SubscriptionStatus.Active, org.SubscriptionStatus);
        Assert.Equal("sub_123", org.StripeSubscriptionId);
        Assert.Equal(25, org.SeatLimit);
    }

    [Fact]
    public void CancelToFree_downgrades_and_clears_subscription()
    {
        var org = Organization.CreateNew("t-1", "Acme", freeSeatLimit: 2, DateTime.UtcNow);
        org.ActivatePro("sub_123", 25);

        org.CancelToFree(freeSeatLimit: 2);

        Assert.Equal(PlanTier.Free, org.Plan);
        Assert.Equal(SubscriptionStatus.Canceled, org.SubscriptionStatus);
        Assert.Null(org.StripeSubscriptionId);
        Assert.Equal(2, org.SeatLimit);
    }
}
