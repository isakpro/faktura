namespace Faktura.Domain.Abstractions;

/// <summary>
/// Throttles repeated failed logins to slow password guessing (FR-023). Keyed by a stable
/// identifier such as the normalized email.
/// </summary>
public interface ILoginThrottle
{
    /// <summary>True if the key is currently locked out; sets <paramref name="retryAfterSeconds"/>.</summary>
    bool IsBlocked(string key, out int retryAfterSeconds);

    /// <summary>Records a failed attempt; may transition the key into a locked-out state.</summary>
    void RecordFailure(string key);

    /// <summary>Clears attempts for the key after a successful login.</summary>
    void Reset(string key);
}
