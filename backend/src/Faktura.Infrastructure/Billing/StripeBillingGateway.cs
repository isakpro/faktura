using Faktura.Domain.Abstractions;
using Faktura.Domain.Organizations;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Faktura.Infrastructure.Billing;

/// <summary>Creates Stripe (test mode) customers and Pro subscription checkout sessions.</summary>
internal sealed class StripeBillingGateway : IBillingGateway
{
    private readonly StripeOptions _options;
    private readonly StripeClient _client;

    public StripeBillingGateway(IOptions<StripeOptions> options)
    {
        _options = options.Value;
        _client = new StripeClient(_options.SecretKey);
    }

    public async Task<CheckoutResult> CreateProCheckoutAsync(Organization organization, string returnUrl, CancellationToken ct = default)
    {
        var customerId = organization.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            var customerService = new CustomerService(_client);
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Name = organization.Name,
                Metadata = new Dictionary<string, string> { ["tenantId"] = organization.Id }
            }, cancellationToken: ct);
            customerId = customer.Id;
        }

        var sessionService = new SessionService(_client);
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = _options.ProPriceId, Quantity = 1 }
            },
            SuccessUrl = returnUrl,
            CancelUrl = returnUrl,
            ClientReferenceId = organization.Id
        }, cancellationToken: ct);

        return new CheckoutResult(session.Url, customerId);
    }
}
