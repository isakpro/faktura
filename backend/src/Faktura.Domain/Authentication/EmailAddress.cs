using System.Text.RegularExpressions;
using Faktura.Domain.Common;

namespace Faktura.Domain.Authentication;

/// <summary>A validated, normalized (trimmed + lowercased) email address.</summary>
public sealed partial class EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static Result<EmailAddress> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Result.Failure<EmailAddress>(Error.Validation("E-post krävs."));

        var normalized = raw.Trim().ToLowerInvariant();

        if (normalized.Length > 254 || !EmailRegex().IsMatch(normalized))
            return Result.Failure<EmailAddress>(Error.Validation("Ogiltig e-postadress."));

        return Result.Success(new EmailAddress(normalized));
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
