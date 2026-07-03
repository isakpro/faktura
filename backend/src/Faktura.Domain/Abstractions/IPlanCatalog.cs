using Faktura.Domain.Organizations;

namespace Faktura.Domain.Abstractions;

/// <summary>Provides plan definitions (seats + rate limits) from configuration.</summary>
public interface IPlanCatalog
{
    PlanDefinition Get(PlanTier tier);
}
