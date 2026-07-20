using System.Security.Cryptography;

namespace Faktura.Domain.Invoicing;

/// <summary>
/// Kapabilitets-token för kundportalen (spec 013): 128 bitar kryptografisk slump som hex.
/// Lagras i klartext (länken måste kunna visas igen); ogörlig att gissa, återkallning utanför scope.
/// </summary>
public static class ShareTokens
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
