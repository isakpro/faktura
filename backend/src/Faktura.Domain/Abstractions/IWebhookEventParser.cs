using Faktura.Domain.Billing;
using Faktura.Domain.Common;

namespace Faktura.Domain.Abstractions;

/// <summary>
/// Verifies a provider webhook's signature and normalizes it to a <see cref="BillingEvent"/>.
/// Returns a failure (invalid_signature) when the payload cannot be verified as authentic.
/// </summary>
public interface IWebhookEventParser
{
    Result<BillingEvent> Parse(string payload, string? signature);
}
