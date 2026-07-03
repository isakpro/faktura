namespace Faktura.Domain.Organizations;

/// <summary>
/// Data-driven description of what a plan grants (seats + rate limit). Sourced from
/// plan configuration, never hard-coded into business logic (constitution V / FR-019).
/// </summary>
public sealed record PlanDefinition(
    PlanTier Tier,
    int SeatLimit,
    int RateLimitPermitLimit,
    int RateLimitWindowSeconds);
