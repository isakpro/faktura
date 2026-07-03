namespace Faktura.Api.Features.Members;

public sealed record MemberDto(string Id, string Email, string Role);
public sealed record InvitationDto(string Id, string Email, string Role, string Status);

public sealed record InviteRequest(string Email, string Role);
public sealed record InviteResponse(InvitationDto Invitation, string Token);

public sealed record AcceptInvitationRequest(string Password);
public sealed record ChangeRoleRequest(string Role);
