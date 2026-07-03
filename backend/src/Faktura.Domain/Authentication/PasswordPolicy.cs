using Faktura.Domain.Common;

namespace Faktura.Domain.Authentication;

/// <summary>Minimum password strength policy (FR-003).</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static Result Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return Result.Failure(Error.WeakPassword($"Lösenordet måste vara minst {MinLength} tecken."));

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        if (!hasLetter || !hasDigit)
            return Result.Failure(Error.WeakPassword("Lösenordet måste innehålla både bokstäver och siffror."));

        return Result.Success();
    }
}
