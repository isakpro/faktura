using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;

namespace Faktura.Domain.Authentication;

/// <summary>The organization and its owner produced by a successful registration.</summary>
public sealed record RegistrationOutcome(Organization Organization, User Owner);

/// <summary>
/// Pure domain logic for self-service registration: validates input and builds a new
/// organization (Free plan) plus its Owner user with a hashed password. Uniqueness of the
/// email is checked by the caller (repository) before invoking this service.
/// </summary>
public sealed class AccountRegistration
{
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;
    private readonly IPlanCatalog _plans;

    public AccountRegistration(IPasswordHasher passwordHasher, IIdGenerator ids, IClock clock, IPlanCatalog plans)
    {
        _passwordHasher = passwordHasher;
        _ids = ids;
        _clock = clock;
        _plans = plans;
    }

    public Result<RegistrationOutcome> Register(string? organizationName, string? rawEmail, string? rawPassword)
    {
        if (string.IsNullOrWhiteSpace(organizationName))
            return Result.Failure<RegistrationOutcome>(Error.Validation("Organisationsnamn krävs."));

        var email = EmailAddress.Create(rawEmail);
        if (email.IsFailure)
            return Result.Failure<RegistrationOutcome>(email.Error);

        var password = PasswordPolicy.Validate(rawPassword);
        if (password.IsFailure)
            return Result.Failure<RegistrationOutcome>(password.Error);

        var now = _clock.UtcNow;
        var freeSeatLimit = _plans.Get(PlanTier.Free).SeatLimit;

        var tenantId = _ids.NewId();
        var organization = Organization.CreateNew(tenantId, organizationName, freeSeatLimit, now);

        var passwordHash = _passwordHasher.Hash(rawPassword!);
        var owner = User.CreateOwner(_ids.NewId(), tenantId, email.Value.Value, passwordHash, now);

        return Result.Success(new RegistrationOutcome(organization, owner));
    }
}
