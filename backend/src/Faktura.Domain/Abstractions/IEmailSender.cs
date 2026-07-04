using Faktura.Domain.Emailing;

namespace Faktura.Domain.Abstractions;

/// <summary>Skickar ett e-postmeddelande. Implementeras med SMTP i Infrastructure; kastar vid fel.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
