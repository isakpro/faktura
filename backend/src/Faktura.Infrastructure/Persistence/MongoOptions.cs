namespace Faktura.Infrastructure.Persistence;

/// <summary>MongoDB connection settings, bound from the "Mongo" section.</summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "faktura";
}
