namespace Faktura.Infrastructure.Configuration;

/// <summary>Data-driven plan configuration, bound from the "Plans" section (overridable).</summary>
public sealed class PlanOptions
{
    public const string SectionName = "Plans";

    public PlanTierOptions Free { get; set; } = new() { SeatLimit = 2, RateLimitPermitLimit = 60, RateLimitWindowSeconds = 60 };
    public PlanTierOptions Pro { get; set; } = new() { SeatLimit = 25, RateLimitPermitLimit = 300, RateLimitWindowSeconds = 60 };
}

public sealed class PlanTierOptions
{
    public int SeatLimit { get; set; }
    public int RateLimitPermitLimit { get; set; }
    public int RateLimitWindowSeconds { get; set; }
}
