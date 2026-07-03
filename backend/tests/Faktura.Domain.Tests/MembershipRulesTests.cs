using Faktura.Domain.Users;
using Xunit;

namespace Faktura.Domain.Tests;

public class MembershipRulesTests
{
    [Theory]
    [InlineData(UserRole.Owner, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Member, false)]
    public void CanManageMembers_allows_owner_and_admin_only(UserRole actor, bool allowed)
    {
        Assert.Equal(allowed, MembershipRules.CanManageMembers(actor).IsSuccess);
    }

    [Fact]
    public void Admin_cannot_grant_owner_role()
    {
        var result = MembershipRules.CanAssignRole(UserRole.Admin, UserRole.Owner, UserRole.Member);
        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public void Admin_cannot_revoke_owner_role()
    {
        var result = MembershipRules.CanAssignRole(UserRole.Admin, UserRole.Member, UserRole.Owner);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Owner_can_assign_owner_role()
    {
        Assert.True(MembershipRules.CanAssignRole(UserRole.Owner, UserRole.Owner, UserRole.Member).IsSuccess);
    }

    [Fact]
    public void Member_cannot_assign_roles()
    {
        Assert.True(MembershipRules.CanAssignRole(UserRole.Member, UserRole.Member, UserRole.Member).IsFailure);
    }

    [Theory]
    [InlineData(1, 2, true)]   // 1 active, limit 2 -> seat available
    [InlineData(2, 2, false)]  // at limit -> blocked
    [InlineData(3, 2, false)]
    public void EnsureSeatAvailable_respects_limit(int active, int limit, bool ok)
    {
        Assert.Equal(ok, MembershipRules.EnsureSeatAvailable(active, limit).IsSuccess);
    }

    [Fact]
    public void EnsureNotRemovingLastOwner_blocks_last_owner()
    {
        Assert.True(MembershipRules.EnsureNotRemovingLastOwner(UserRole.Owner, ownerCount: 1).IsFailure);
        Assert.True(MembershipRules.EnsureNotRemovingLastOwner(UserRole.Owner, ownerCount: 2).IsSuccess);
        Assert.True(MembershipRules.EnsureNotRemovingLastOwner(UserRole.Member, ownerCount: 1).IsSuccess);
    }
}
