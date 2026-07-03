namespace Faktura.Domain.Abstractions;

/// <summary>Abstraction over the system clock so time-dependent logic is testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
