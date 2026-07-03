using Faktura.Domain.Users;
using Xunit;

namespace Faktura.Domain.Tests;

public class InvitationTests
{
    private static readonly DateTime Now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateNew_pending_with_expiry()
    {
        var inv = Invitation.CreateNew("i-1", "t-1", "kollega@acme.se", UserRole.Member, "hash", Now, TimeSpan.FromDays(7));

        Assert.True(inv.IsSuccess);
        Assert.Equal(InvitationStatus.Pending, inv.Value.Status);
        Assert.Equal(Now.AddDays(7), inv.Value.ExpiresAt);
    }

    [Fact]
    public void CreateNew_rejects_owner_role()
    {
        Assert.True(Invitation.CreateNew("i", "t", "a@b.se", UserRole.Owner, "h", Now, TimeSpan.FromDays(7)).IsFailure);
    }

    [Fact]
    public void Accept_succeeds_once_then_fails()
    {
        var inv = Invitation.CreateNew("i", "t", "a@b.se", UserRole.Member, "h", Now, TimeSpan.FromDays(7)).Value;

        Assert.True(inv.Accept(Now).IsSuccess);
        Assert.Equal(InvitationStatus.Accepted, inv.Status);
        Assert.True(inv.Accept(Now).IsFailure); // already accepted
    }

    [Fact]
    public void Accept_fails_when_expired()
    {
        var inv = Invitation.CreateNew("i", "t", "a@b.se", UserRole.Member, "h", Now, TimeSpan.FromDays(7)).Value;
        Assert.True(inv.Accept(Now.AddDays(8)).IsFailure);
    }

    [Fact]
    public void Revoke_prevents_acceptance()
    {
        var inv = Invitation.CreateNew("i", "t", "a@b.se", UserRole.Member, "h", Now, TimeSpan.FromDays(7)).Value;
        inv.Revoke();
        Assert.Equal(InvitationStatus.Revoked, inv.Status);
        Assert.True(inv.Accept(Now).IsFailure);
    }
}
