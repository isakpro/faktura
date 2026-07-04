using Faktura.Domain.Abstractions;
using Faktura.Domain.Emailing;
using Faktura.Domain.Invoicing;

namespace Faktura.Api.Tests.Fakes;

/// <summary>
/// Fejkad e-postsändare: fångar senaste meddelandet och kastar för mottagare som börjar med
/// "fail@" (för att testa leveransfel) — inga riktiga mejl.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    public EmailMessage? LastMessage { get; private set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        LastMessage = message;
        if (message.To.StartsWith("fail@", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SMTP fail (test)");
        return Task.CompletedTask;
    }
}

public sealed class InMemoryInvoiceEmailRepository : IInvoiceEmailRepository
{
    private readonly List<InvoiceEmail> _items = new();

    public Task AddAsync(InvoiceEmail email, CancellationToken ct = default)
    {
        lock (_items) _items.Add(email);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InvoiceEmail>> ListByInvoiceAsync(string tenantId, string invoiceId, CancellationToken ct = default)
    {
        lock (_items)
            return Task.FromResult<IReadOnlyList<InvoiceEmail>>(
                _items.Where(e => e.TenantId == tenantId && e.InvoiceId == invoiceId)
                    .OrderByDescending(e => e.SentAt).ToList());
    }
}
