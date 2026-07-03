using Faktura.Domain.Abstractions;

namespace Faktura.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
