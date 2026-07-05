using Faktura.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faktura.Api.Health;

/// <summary>Readiness: pingar MongoDB. Registreras inte i Testing (in-memory-repos där).</summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly MongoContext _context;

    public MongoHealthCheck(MongoContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await _context.PingAsync(ct);
            return HealthCheckResult.Healthy("MongoDB svarar.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB svarar inte.", ex);
        }
    }
}
