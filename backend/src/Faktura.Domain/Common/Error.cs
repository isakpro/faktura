namespace Faktura.Domain.Common;

/// <summary>
/// A domain error with a stable machine-readable <see cref="Code"/> and a human message.
/// Codes map to HTTP problem responses in the API layer.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("", "");

    // Reusable errors for the SaaS skeleton.
    public static Error Validation(string message) => new("validation", message);
    public static Error EmailAlreadyInUse() => new("email_in_use", "E-postadressen är redan registrerad.");
    public static Error InvalidCredentials() => new("invalid_credentials", "Fel e-post eller lösenord.");
    public static Error WeakPassword(string message) => new("weak_password", message);
}
