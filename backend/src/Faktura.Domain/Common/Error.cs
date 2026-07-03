namespace Faktura.Domain.Common;

/// <summary>
/// A domain error with a stable machine-readable <see cref="Code"/> and a human message.
/// Codes map to HTTP problem responses in the API layer. <see cref="RetryAfterSeconds"/>
/// is set for throttling/rate-limit errors that produce a 429 with a Retry-After header.
/// </summary>
public sealed record Error(string Code, string Message, int? RetryAfterSeconds = null)
{
    public static readonly Error None = new("", "");

    // Reusable errors for the SaaS skeleton.
    public static Error Validation(string message) => new("validation", message);
    public static Error EmailAlreadyInUse() => new("email_in_use", "E-postadressen är redan registrerad.");
    public static Error InvalidCredentials() => new("invalid_credentials", "Fel e-post eller lösenord.");
    public static Error WeakPassword(string message) => new("weak_password", message);
    public static Error TooManyAttempts(int retryAfterSeconds) =>
        new("too_many_attempts", "För många inloggningsförsök. Försök igen senare.", retryAfterSeconds);

    // Members / roles / plan (US3).
    public static Error Forbidden() => new("forbidden", "Otillräcklig behörighet för åtgärden.");
    public static Error NotFound() => new("not_found", "Resursen hittades inte.");
    public static Error SeatLimitReached() => new("seat_limit", "Plangränsen för antal användare är nådd. Uppgradera till Pro.");
    public static Error LastOwner() => new("last_owner", "Organisationen måste ha minst en Owner.");
    public static Error InvitationInvalid() => new("invitation_invalid", "Inbjudan är ogiltig eller har gått ut.");
}
