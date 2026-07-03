using Faktura.Domain.Common;

namespace Faktura.Domain.Users;

/// <summary>
/// Pure RBAC and membership invariants (US3). These are enforced server-side; the API
/// layer supplies the counts (seats, owners) from repositories.
/// </summary>
public static class MembershipRules
{
    /// <summary>Owner and Admin may manage members/invitations; Member may not.</summary>
    public static Result CanManageMembers(UserRole actor) =>
        actor is UserRole.Owner or UserRole.Admin
            ? Result.Success()
            : Result.Failure(Error.Forbidden());

    /// <summary>
    /// Owner/Admin may change roles, but only an Owner may grant or revoke the Owner role.
    /// </summary>
    public static Result CanAssignRole(UserRole actor, UserRole newRole, UserRole currentTargetRole)
    {
        if (actor is not (UserRole.Owner or UserRole.Admin))
            return Result.Failure(Error.Forbidden());

        var involvesOwner = newRole == UserRole.Owner || currentTargetRole == UserRole.Owner;
        if (involvesOwner && actor != UserRole.Owner)
            return Result.Failure(Error.Forbidden());

        return Result.Success();
    }

    /// <summary>A tenant may not exceed its plan's seat limit (FR-025).</summary>
    public static Result EnsureSeatAvailable(int activeUsers, int seatLimit) =>
        activeUsers < seatLimit
            ? Result.Success()
            : Result.Failure(Error.SeatLimitReached());

    /// <summary>The organization must always keep at least one Owner (FR-013).</summary>
    public static Result EnsureNotRemovingLastOwner(UserRole targetCurrentRole, int ownerCount) =>
        targetCurrentRole == UserRole.Owner && ownerCount <= 1
            ? Result.Failure(Error.LastOwner())
            : Result.Success();
}
