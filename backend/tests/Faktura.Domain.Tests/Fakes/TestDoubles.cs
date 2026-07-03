using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;

namespace Faktura.Domain.Tests.Fakes;

/// <summary>Fixed clock for deterministic tests.</summary>
public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

/// <summary>Deterministic id generator: id-1, id-2, ...</summary>
public sealed class SequentialIdGenerator : IIdGenerator
{
    private int _n;
    public string NewId() => $"id-{++_n}";
}

/// <summary>Reversible "hash" so tests can assert hashing happened without real crypto.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string hash, string password) => hash == $"hashed:{password}";
}

/// <summary>In-memory plan catalog with the default Free/Pro tiers.</summary>
public sealed class TestPlanCatalog : IPlanCatalog
{
    public PlanDefinition Get(PlanTier tier) => tier switch
    {
        PlanTier.Pro => new PlanDefinition(PlanTier.Pro, SeatLimit: 25, RateLimitPermitLimit: 300, RateLimitWindowSeconds: 60),
        _ => new PlanDefinition(PlanTier.Free, SeatLimit: 2, RateLimitPermitLimit: 60, RateLimitWindowSeconds: 60)
    };
}
