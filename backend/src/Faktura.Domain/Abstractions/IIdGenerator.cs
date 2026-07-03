namespace Faktura.Domain.Abstractions;

/// <summary>Generates opaque, unique identifiers for new entities.</summary>
public interface IIdGenerator
{
    string NewId();
}
