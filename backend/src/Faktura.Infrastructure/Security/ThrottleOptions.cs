namespace Faktura.Infrastructure.Security;

/// <summary>Login throttling configuration, bound from the "Throttle" section.</summary>
public sealed class ThrottleOptions
{
    public const string SectionName = "Throttle";

    /// <summary>Failed attempts allowed within the window before lockout.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Sliding window (seconds) over which failures are counted.</summary>
    public int WindowSeconds { get; set; } = 900;

    /// <summary>Lockout duration (seconds) once the limit is exceeded.</summary>
    public int LockoutSeconds { get; set; } = 900;
}
