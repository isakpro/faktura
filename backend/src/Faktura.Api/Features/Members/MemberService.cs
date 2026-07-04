using Faktura.Api.Features.Auth;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Common;
using Faktura.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Faktura.Api.Features.Members;

/// <summary>
/// Application service for members, invitations and role changes. All operations are
/// scoped to the caller's tenant (from <see cref="ITenantContext"/>) and enforce RBAC and
/// seat/owner invariants via <see cref="MembershipRules"/>.
/// </summary>
public sealed class MemberService
{
    private static readonly TimeSpan InvitationTtl = TimeSpan.FromDays(7);

    private readonly ITenantContext _tenant;
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;
    private readonly IOrganizationRepository _organizations;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly TokenIssuer _issuer;
    private readonly ILogger<MemberService> _logger;

    public MemberService(
        ITenantContext tenant,
        IUserRepository users,
        IInvitationRepository invitations,
        IOrganizationRepository organizations,
        ITokenService tokens,
        IPasswordHasher passwordHasher,
        IIdGenerator ids,
        IClock clock,
        TokenIssuer issuer,
        ILogger<MemberService> logger)
    {
        _tenant = tenant;
        _users = users;
        _invitations = invitations;
        _organizations = organizations;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _ids = ids;
        _clock = clock;
        _issuer = issuer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken ct)
    {
        var users = await _users.ListByTenantAsync(_tenant.TenantId, ct);
        return users.Select(u => new MemberDto(u.Id, u.Email, u.Role.ToString())).ToList();
    }

    public async Task<IReadOnlyList<InvitationDto>> ListInvitationsAsync(CancellationToken ct)
    {
        var invites = await _invitations.ListByTenantAsync(_tenant.TenantId, ct);
        return invites.Select(ToDto).ToList();
    }

    public async Task<Result<InviteResponse>> InviteAsync(InviteRequest request, CancellationToken ct)
    {
        var manage = MembershipRules.CanManageMembers(_tenant.Role);
        if (manage.IsFailure) return Result.Failure<InviteResponse>(manage.Error);

        if (!TryParseAssignableRole(request.Role, out var role))
            return Result.Failure<InviteResponse>(Error.Validation("Ogiltig roll."));

        var email = EmailAddress.Create(request.Email);
        if (email.IsFailure) return Result.Failure<InviteResponse>(email.Error);
        var normalizedEmail = email.Value.Value;

        var organization = await _organizations.GetByIdAsync(_tenant.TenantId, ct);
        if (organization is null) return Result.Failure<InviteResponse>(Error.NotFound());

        var seatCount = await _users.CountByTenantAsync(_tenant.TenantId, ct);
        var seat = MembershipRules.EnsureSeatAvailable(seatCount, organization.SeatLimit);
        if (seat.IsFailure) return Result.Failure<InviteResponse>(seat.Error);

        if (await _users.EmailExistsAsync(normalizedEmail, ct))
            return Result.Failure<InviteResponse>(Error.EmailAlreadyInUse());
        if (await _invitations.HasPendingForEmailAsync(_tenant.TenantId, normalizedEmail, ct))
            return Result.Failure<InviteResponse>(Error.Validation("En inbjudan är redan skickad till adressen."));

        var token = _tokens.CreateRefreshToken(); // random opaque token + hash
        var invitation = Invitation.CreateNew(
            _ids.NewId(), _tenant.TenantId, normalizedEmail, role, token.Hash, _clock.UtcNow, InvitationTtl);
        if (invitation.IsFailure) return Result.Failure<InviteResponse>(invitation.Error);

        await _invitations.AddAsync(invitation.Value, ct);
        _logger.LogInformation("Invitation created for {Email} in tenant {TenantId}", normalizedEmail, _tenant.TenantId);
        return Result.Success(new InviteResponse(ToDto(invitation.Value), token.Raw));
    }

    public async Task<Result<AuthResponse>> AcceptAsync(string rawToken, AcceptInvitationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return Result.Failure<AuthResponse>(Error.InvitationInvalid());

        var invitation = await _invitations.GetByTokenHashAsync(_tokens.HashRefreshToken(rawToken), ct);
        if (invitation is null || !invitation.IsAcceptable(_clock.UtcNow))
            return Result.Failure<AuthResponse>(Error.InvitationInvalid());

        var passwordCheck = PasswordPolicy.Validate(request.Password);
        if (passwordCheck.IsFailure) return Result.Failure<AuthResponse>(passwordCheck.Error);

        var organization = await _organizations.GetByIdAsync(invitation.TenantId, ct);
        if (organization is null) return Result.Failure<AuthResponse>(Error.InvitationInvalid());

        var seatCount = await _users.CountByTenantAsync(invitation.TenantId, ct);
        var seat = MembershipRules.EnsureSeatAvailable(seatCount, organization.SeatLimit);
        if (seat.IsFailure) return Result.Failure<AuthResponse>(seat.Error);

        if (await _users.EmailExistsAsync(invitation.Email, ct))
            return Result.Failure<AuthResponse>(Error.EmailAlreadyInUse());

        var user = User.CreateMember(
            _ids.NewId(), invitation.TenantId, invitation.Email,
            _passwordHasher.Hash(request.Password), invitation.Role, _clock.UtcNow);
        await _users.AddAsync(user, ct);

        invitation.Accept(_clock.UtcNow);
        await _invitations.UpdateAsync(invitation, ct);

        _logger.LogInformation("Invitation accepted; user {UserId} joined tenant {TenantId}", user.Id, invitation.TenantId);
        return Result.Success(await _issuer.IssueAsync(user, organization, ct));
    }

    public async Task<Result> RevokeInvitationAsync(string invitationId, CancellationToken ct)
    {
        var manage = MembershipRules.CanManageMembers(_tenant.Role);
        if (manage.IsFailure) return manage;

        var invitation = await _invitations.GetByIdAsync(_tenant.TenantId, invitationId, ct);
        if (invitation is null) return Result.Failure(Error.NotFound());

        invitation.Revoke();
        await _invitations.UpdateAsync(invitation, ct);
        return Result.Success();
    }

    public async Task<Result<MemberDto>> ChangeRoleAsync(string targetUserId, ChangeRoleRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
            return Result.Failure<MemberDto>(Error.Validation("Ogiltig roll."));

        var target = await _users.GetByIdAsync(_tenant.TenantId, targetUserId, ct);
        if (target is null) return Result.Failure<MemberDto>(Error.NotFound());

        var canAssign = MembershipRules.CanAssignRole(_tenant.Role, newRole, target.Role);
        if (canAssign.IsFailure) return Result.Failure<MemberDto>(canAssign.Error);

        // Demoting the last remaining Owner is not allowed.
        if (target.Role == UserRole.Owner && newRole != UserRole.Owner)
        {
            var ownerCount = await _users.CountOwnersAsync(_tenant.TenantId, ct);
            var lastOwner = MembershipRules.EnsureNotRemovingLastOwner(target.Role, ownerCount);
            if (lastOwner.IsFailure) return Result.Failure<MemberDto>(lastOwner.Error);
        }

        target.ChangeRole(newRole);
        await _users.UpdateAsync(target, ct);
        return Result.Success(new MemberDto(target.Id, target.Email, target.Role.ToString()));
    }

    public async Task<Result> RemoveMemberAsync(string targetUserId, CancellationToken ct)
    {
        var target = await _users.GetByIdAsync(_tenant.TenantId, targetUserId, ct);
        if (target is null) return Result.Failure(Error.NotFound());

        var canRemove = MembershipRules.CanRemoveMember(_tenant.Role, target.Role);
        if (canRemove.IsFailure) return canRemove;

        if (target.Role == UserRole.Owner)
        {
            var ownerCount = await _users.CountOwnersAsync(_tenant.TenantId, ct);
            var lastOwner = MembershipRules.EnsureNotRemovingLastOwner(target.Role, ownerCount);
            if (lastOwner.IsFailure) return lastOwner;
        }

        // Refresh-tokens dör automatiskt: RefreshAsync slår upp användaren och nekar när den saknas.
        await _users.RemoveAsync(_tenant.TenantId, targetUserId, ct);
        _logger.LogInformation("Member {UserId} removed from tenant {TenantId}", targetUserId, _tenant.TenantId);
        return Result.Success();
    }

    private static InvitationDto ToDto(Invitation i) => new(i.Id, i.Email, i.Role.ToString(), i.Status.ToString());

    private static bool TryParseAssignableRole(string value, out UserRole role)
    {
        // Owner cannot be invited/assigned via invitation.
        return Enum.TryParse(value, ignoreCase: true, out role) && role is UserRole.Admin or UserRole.Member;
    }
}
