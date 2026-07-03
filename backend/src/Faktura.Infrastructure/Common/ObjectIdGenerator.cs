using Faktura.Domain.Abstractions;
using MongoDB.Bson;

namespace Faktura.Infrastructure.Common;

/// <summary>Generates ids as MongoDB ObjectId strings.</summary>
public sealed class ObjectIdGenerator : IIdGenerator
{
    public string NewId() => ObjectId.GenerateNewId().ToString();
}
