namespace Faktura.Infrastructure.Security;

/// <summary>JWT configuration, bound from the "Jwt" section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "faktura";
    public string Audience { get; set; } = "faktura";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
