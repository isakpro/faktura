namespace Faktura.Domain.Abstractions;

/// <summary>Hashes and verifies passwords. Implemented with PBKDF2 in Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Constant-time verification of <paramref name="password"/> against <paramref name="hash"/>.</summary>
    bool Verify(string hash, string password);
}
