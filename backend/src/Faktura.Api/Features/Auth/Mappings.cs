using Faktura.Domain.Organizations;
using Faktura.Domain.Users;

namespace Faktura.Api.Features.Auth;

/// <summary>Maps domain entities to response DTOs.</summary>
public static class Mappings
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.Role.ToString());

    public static OrganizationDto ToDto(this Organization o) =>
        new(o.Id, o.Name, o.Plan.ToString(), o.SubscriptionStatus.ToString(), o.SeatLimit);
}
