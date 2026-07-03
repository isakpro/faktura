using Faktura.Domain.Organizations;

namespace Faktura.Domain.Abstractions;

/// <summary>Result of starting a checkout: the URL to redirect to, and the Stripe customer id.</summary>
public readonly record struct CheckoutResult(string CheckoutUrl, string CustomerId);

/// <summary>Creates provider (Stripe) checkout sessions for the Pro subscription.</summary>
public interface IBillingGateway
{
    /// <summary>
    /// Ensures a customer exists for the organization and starts a Pro subscription checkout.
    /// Returns the checkout URL and the (possibly newly created) customer id.
    /// </summary>
    Task<CheckoutResult> CreateProCheckoutAsync(Organization organization, string returnUrl, CancellationToken ct = default);
}
