namespace Faktura.Api.Features.Auth;

// Requests
public sealed record RegisterRequest(string OrganizationName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);

// Responses
public sealed record UserDto(string Id, string Email, string Role);
public sealed record OrganizationDto(string Id, string Name, string Plan, string SubscriptionStatus, int SeatLimit);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User,
    OrganizationDto Organization);

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public sealed record MeResponse(UserDto User, OrganizationDto Organization);
