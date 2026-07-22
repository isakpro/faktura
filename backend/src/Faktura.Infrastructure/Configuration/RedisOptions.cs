namespace Faktura.Infrastructure.Configuration;

/// <summary>Redis-anslutning för distribuerad rate limiting/broms (spec 018).</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
}
