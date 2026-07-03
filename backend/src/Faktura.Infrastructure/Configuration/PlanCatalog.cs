using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;
using Microsoft.Extensions.Options;

namespace Faktura.Infrastructure.Configuration;

/// <summary>Serves plan definitions from <see cref="PlanOptions"/> configuration.</summary>
public sealed class PlanCatalog : IPlanCatalog
{
    private readonly PlanOptions _options;

    public PlanCatalog(IOptions<PlanOptions> options) => _options = options.Value;

    public PlanDefinition Get(PlanTier tier)
    {
        var t = tier == PlanTier.Pro ? _options.Pro : _options.Free;
        return new PlanDefinition(tier, t.SeatLimit, t.RateLimitPermitLimit, t.RateLimitWindowSeconds);
    }
}
